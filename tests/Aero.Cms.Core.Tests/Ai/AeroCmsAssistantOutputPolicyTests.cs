using System.Runtime.CompilerServices;
using System.Security.Claims;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Budget;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Cms.Modules.AiAssistant;
using Aero.Cms.Modules.AiAssistant.Pipeline;
using Aero.Core;
using Aero.Core.Ai;
using Aero.Core.Railway;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroCmsAssistantOutputPolicyTests
{
    private readonly AeroCmsAssistantOutputPolicy _policy = new();

    [Test]
    public async Task Public_output_requires_only_server_supplied_citations()
    {
        var citations = new[]
        {
            new AeroCmsAssistantCitation(
                "CMS-1",
                "page",
                "41",
                "/care",
                "Animal care",
                "Body")
        };

        var accepted = _policy.Evaluate(
            new(AeroAiAudience.Public, "Feed twice daily. [CMS-1]", citations));
        var missing = _policy.Evaluate(
            new(AeroAiAudience.Public, "Feed twice daily.", citations));
        var invented = _policy.Evaluate(
            new(AeroAiAudience.Member, "Feed twice daily. [CMS-2]", citations));

        accepted.ShouldBeOfType<Result<string>.Ok>();
        missing.ShouldBeOfType<Result<string>.Failure>();
        invented.ShouldBeOfType<Result<string>.Failure>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Output_policy_rejects_secrets_and_high_risk_identifiers_without_blocking_public_contact_text()
    {
        var privateKey = _policy.Evaluate(
            new(AeroAiAudience.Manager, "-----BEGIN PRIVATE KEY-----\nabc", []));
        var bearer = _policy.Evaluate(
            new(AeroAiAudience.Manager, "Authorization: Bearer abcdefghijklmnop", []));
        var socialSecurityNumber = _policy.Evaluate(
            new(AeroAiAudience.Manager, "The value is 123-45-6789.", []));
        var paymentCard = _policy.Evaluate(
            new(AeroAiAudience.Manager, "Use 4111 1111 1111 1111.", []));
        var ordinaryContact = _policy.Evaluate(
            new(AeroAiAudience.Manager, "Email support@example.test or call 312-555-0188.", []));

        privateKey.ShouldBeOfType<Result<string>.Failure>();
        bearer.ShouldBeOfType<Result<string>.Failure>();
        socialSecurityNumber.ShouldBeOfType<Result<string>.Failure>();
        paymentCard.ShouldBeOfType<Result<string>.Failure>();
        ordinaryContact.ShouldBeOfType<Result<string>.Ok>();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Streaming_buffers_provider_output_until_policy_approval_and_emits_no_unsafe_delta()
    {
        var explicitMemoryStore = new EmptyExplicitMemoryStore();
        var grounding = new AeroCmsAssistantGroundingService(
            new SingleMatchRetriever(),
            explicitMemoryStore,
            NullLogger<AeroCmsAssistantGroundingService>.Instance);
        var budget = new AeroAiTokenBudgetCoordinator(
            Options.Create(new AeroAiTokenBudgetOptions
            {
                TokenLimitPerPartition = 100_000,
                MaximumReservationTokens = 32_768
            }),
            TimeProvider.System);
        var service = new AeroCmsAssistantService(
            new FixedSettingsProvider(),
            new StreamingChatClientFactory(),
            [],
            new UnusedConversationStore(),
            grounding,
            new AeroAiRequestPipeline([]),
            _policy,
            budget,
            NullLogger<AeroCmsAssistantService>.Instance);
        var context = new AeroCmsSiteAssistantContext(
            AeroAiAudience.Public,
            new ClaimsPrincipal(new ClaimsIdentity()),
            PrincipalId: 0,
            TenantId: 41,
            SiteId: 73,
            Culture: "en-US",
            CorrelationId: "stream-policy-test");

        var result = await service.StreamAsync(
            new AeroCmsAssistantRequest([new(AeroCmsAssistantRole.User, "How should I feed it?")]),
            context);
        var events = new List<AeroCmsAssistantEvent>();
        await foreach (var item in result
                           .ShouldBeOfType<Result<IAsyncEnumerable<AeroCmsAssistantEvent>>.Ok>()
                           .Value)
        {
            events.Add(item);
        }

        events.Select(item => item.Kind).ShouldBe(
            [AeroCmsAssistantEventKind.Metadata, AeroCmsAssistantEventKind.Error]);
        events.ShouldNotContain(item => item.Kind == AeroCmsAssistantEventKind.Delta);
        events.ShouldNotContain(item => item.Kind == AeroCmsAssistantEventKind.Complete);
    }

    private sealed class FixedSettingsProvider : IAiSettingsProvider
    {
        public Task<Result<AiRuntimeSettings>> GetAsync(
            string? providerId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<AiRuntimeSettings>>(new AiRuntimeSettings(
                "provider",
                "Provider",
                Enabled: true,
                AiProviderKind.OpenAi,
                Endpoint: null,
                Model: "model",
                ApiKey: "test-only",
                Temperature: 0,
                MaxOutputTokens: 256,
                TimeoutSeconds: 30,
                StreamResponses: true,
                SaveUsageTelemetry: false,
                SupportsContentEnhancement: true));
    }

    private sealed class StreamingChatClientFactory : IAiChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(
            AiRuntimeSettings settings,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<IChatClient>>(new UnsafeStreamingChatClient());
    }

    private sealed class UnsafeStreamingChatClient : IChatClient
    {
        public void Dispose()
        {
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Bearer abcdefghijklmnop");
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, " [CMS-1]");
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(ChatClientMetadata)
                ? new ChatClientMetadata("test", null, "model")
                : null;
    }

    private sealed class SingleMatchRetriever : IAeroAiKnowledgeRetriever
    {
        public Task<Result<IReadOnlyList<AeroAiKnowledgeMatch>>> SearchAsync(
            AeroAiKnowledgeQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<IReadOnlyList<AeroAiKnowledgeMatch>>>(
                new[]
                {
                    new AeroAiKnowledgeMatch(
                        1,
                        AeroAiKnowledgeSourceKinds.Page,
                        2,
                        "/care",
                        "en-US",
                        "Care",
                        "Body",
                        "Feed twice daily.",
                        1,
                        0,
                        "hash")
                });
    }

    private sealed class EmptyExplicitMemoryStore : IAeroAiExplicitMemoryStore
    {
        public Task<Result<AeroAiExplicitMemory>> SaveAsync(
            AeroAiMemoryScope scope,
            AeroAiExplicitMemoryWrite memory,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<AeroAiExplicitMemory>>> ListAsync(
            AeroAiMemoryScope scope,
            int take = 20,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Result<IReadOnlyList<AeroAiExplicitMemory>>>(
                Array.Empty<AeroAiExplicitMemory>());

        public Task<Result<bool>> DeleteAsync(
            AeroAiMemoryScope scope,
            long memoryId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class UnusedConversationStore : IAeroAiConversationStore
    {
        public Task<Result<AeroAiConversationTurn>> BeginTurnAsync(
            AeroAiMemoryScope scope,
            long? conversationId,
            IReadOnlyList<AeroCmsAssistantMessage> requestMessages,
            string correlationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<bool>> AppendAssistantMessageAsync(
            AeroAiMemoryScope scope,
            long conversationId,
            string content,
            string correlationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>> ListAsync(
            AeroAiMemoryScope scope,
            int take = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<AeroCmsAssistantConversation>> GetAsync(
            AeroAiMemoryScope scope,
            long conversationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<bool>> DeleteAsync(
            AeroAiMemoryScope scope,
            long conversationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
