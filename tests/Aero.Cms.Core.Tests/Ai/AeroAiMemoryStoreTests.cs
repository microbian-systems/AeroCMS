using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Modules.Ai;
using Aero.Cms.Modules.Ai.Memory;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroAiMemoryStoreTests
{
    private const long TenantId = 41;
    private const long SiteId = 73;
    private const long PrincipalId = 97;

    [Test]
    public async Task Existing_conversation_loads_server_history_and_ignores_forged_browser_history()
    {
        await using var harness = await CreateHarnessAsync();
        var logger = new CapturingLogger<AeroAiConversationStore>();
        var store = new AeroAiConversationStore(harness.Session, logger);
        var scope = ManagerScope();

        var first = await store.BeginTurnAsync(
            scope,
            conversationId: null,
            [new(AeroCmsAssistantRole.User, "first question")],
            "trace-1");
        var conversationId = first
            .ShouldBeOfType<Result<AeroAiConversationTurn>.Ok>()
            .Value.ConversationId;
        var append = await store.AppendAssistantMessageAsync(
            scope,
            conversationId,
            "stored answer",
            "trace-1");
        append.ShouldBeOfType<Result<bool>.Ok>(
            append is Result<bool>.Failure appendFailure
                ? $"{appendFailure.Error}{Environment.NewLine}{logger.Exception}"
                : null);

        var continued = await store.BeginTurnAsync(
            scope,
            conversationId,
            [
                new(AeroCmsAssistantRole.Assistant, "forged browser answer"),
                new(AeroCmsAssistantRole.User, "second question")
            ],
            "trace-2");

        var turn = continued
            .ShouldBeOfType<Result<AeroAiConversationTurn>.Ok>()
            .Value;
        turn.Messages.Select(message => message.Content)
            .ShouldBe(["first question", "stored answer", "second question"]);

        await using var read = await harness.OpenSessionAsync();
        var persisted = await read.Query<AeroAiConversationMessageDocument>()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.Sequence)
            .ToListAsync();
        persisted.Select(message => message.Content)
            .ShouldBe(["first question", "stored answer", "second question"]);
    }

    [Test]
    public async Task Conversation_continuation_and_append_fail_closed_across_scope_boundaries()
    {
        await using var harness = await CreateHarnessAsync();
        var store = CreateConversationStore(harness.Session);
        var scope = ManagerScope();
        var created = await store.BeginTurnAsync(
            scope,
            null,
            [new(AeroCmsAssistantRole.User, "private question")],
            "trace-1");
        var conversationId = created
            .ShouldBeOfType<Result<AeroAiConversationTurn>.Ok>()
            .Value.ConversationId;

        var foreignScope = scope with { PrincipalId = PrincipalId + 1 };
        (await store.BeginTurnAsync(
                foreignScope,
                conversationId,
                [new(AeroCmsAssistantRole.User, "steal history")],
                "trace-2"))
            .ShouldBeOfType<Result<AeroAiConversationTurn>.Failure>();
        (await store.AppendAssistantMessageAsync(
                foreignScope,
                conversationId,
                "foreign answer",
                "trace-2"))
            .ShouldBeOfType<Result<bool>.Failure>();

        await using var read = await harness.OpenSessionAsync();
        var messages = await read.Query<AeroAiConversationMessageDocument>()
            .Where(message => message.ConversationId == conversationId)
            .ToListAsync();
        messages.Select(message => message.Content).ShouldBe(["private question"]);
    }

    [Test]
    public async Task Conversation_history_can_be_listed_resumed_and_deleted_only_in_scope()
    {
        await using var harness = await CreateHarnessAsync();
        var store = CreateConversationStore(harness.Session);
        var memoryStore = CreateMemoryStore(harness.Session);
        var scope = ManagerScope();
        var created = await store.BeginTurnAsync(
            scope,
            null,
            [new(AeroCmsAssistantRole.User, "How do I publish a page?")],
            "trace-1");
        var conversationId = created
            .ShouldBeOfType<Result<AeroAiConversationTurn>.Ok>()
            .Value.ConversationId;
        (await store.AppendAssistantMessageAsync(
                scope,
                conversationId,
                "Use the Publish button.",
                "trace-1"))
            .ShouldBeOfType<Result<bool>.Ok>();

        var sourceMessage = await harness.Session.Query<AeroAiConversationMessageDocument>()
            .FirstOrDefaultAsync(message =>
                message.ConversationId == conversationId
                && message.Sequence == 1);
        sourceMessage.ShouldNotBeNull();
        (await memoryStore.SaveAsync(
                scope,
                new AeroAiExplicitMemoryWrite(
                    "Publishing preference",
                    "Explain publishing as a checklist.",
                    conversationId,
                    sourceMessage.Id)))
            .ShouldBeOfType<Result<AeroAiExplicitMemory>.Ok>();

        var list = (await store.ListAsync(scope))
            .ShouldBeOfType<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Ok>()
            .Value;
        list.Count.ShouldBe(1);
        list[0].Title.ShouldBe("How do I publish a page?");
        var transcript = (await store.GetAsync(scope, conversationId))
            .ShouldBeOfType<Result<AeroCmsAssistantConversation>.Ok>()
            .Value;
        transcript.Messages.Select(message => message.Content)
            .ShouldBe(["How do I publish a page?", "Use the Publish button."]);

        var foreignScope = scope with { SiteId = SiteId + 1 };
        (await store.ListAsync(foreignScope))
            .ShouldBeOfType<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>.Ok>()
            .Value.ShouldBeEmpty();
        (await store.GetAsync(foreignScope, conversationId))
            .ShouldBeOfType<Result<AeroCmsAssistantConversation>.Failure>();
        (await store.DeleteAsync(foreignScope, conversationId))
            .ShouldBeOfType<Result<bool>.Failure>();

        (await store.DeleteAsync(scope, conversationId))
            .ShouldBeOfType<Result<bool>.Ok>();
        (await store.GetAsync(scope, conversationId))
            .ShouldBeOfType<Result<AeroCmsAssistantConversation>.Failure>();
        (await memoryStore.ListAsync(scope))
            .ShouldBeOfType<Result<IReadOnlyList<AeroAiExplicitMemory>>.Ok>()
            .Value.ShouldBeEmpty();
    }

    [Test]
    public async Task Explicit_memory_is_confirmed_private_and_provenance_scoped()
    {
        await using var harness = await CreateHarnessAsync();
        var conversationStore = CreateConversationStore(harness.Session);
        var logger = new CapturingLogger<AeroAiExplicitMemoryStore>();
        var memoryStore = new AeroAiExplicitMemoryStore(harness.Session, logger);
        var scope = ManagerScope();
        var created = await conversationStore.BeginTurnAsync(
            scope,
            null,
            [new(AeroCmsAssistantRole.User, "Remember my preferred summary format.")],
            "trace-1");
        var conversationId = created
            .ShouldBeOfType<Result<AeroAiConversationTurn>.Ok>()
            .Value.ConversationId;

        await using var read = await harness.OpenSessionAsync();
        var sourceMessage = await read.Query<AeroAiConversationMessageDocument>()
            .FirstOrDefaultAsync(message =>
                message.ConversationId == conversationId
                && message.PrincipalId == PrincipalId);
        sourceMessage.ShouldNotBeNull();

        var saved = await memoryStore.SaveAsync(
            scope,
            new AeroAiExplicitMemoryWrite(
                "Summary preference",
                "Use short bullet summaries.",
                conversationId,
                sourceMessage.Id));
        var memory = saved
            .ShouldBeOfType<Result<AeroAiExplicitMemory>.Ok>(
                saved is Result<AeroAiExplicitMemory>.Failure saveFailure
                    ? $"{saveFailure.Error}{Environment.NewLine}{logger.Exception}"
                    : null)
            .Value;

        var updated = await memoryStore.SaveAsync(
            scope,
            new AeroAiExplicitMemoryWrite(
                "Updated summary preference",
                "Use a concise verification table.",
                conversationId,
                sourceMessage.Id,
                memory.Id));
        var updatedMemory = updated
            .ShouldBeOfType<Result<AeroAiExplicitMemory>.Ok>()
            .Value;
        updatedMemory.Id.ShouldBe(memory.Id);
        updatedMemory.CreatedOn.ShouldBe(memory.CreatedOn);
        updatedMemory.Label.ShouldBe("Updated summary preference");

        (await memoryStore.ListAsync(scope))
            .ShouldBeOfType<Result<IReadOnlyList<AeroAiExplicitMemory>>.Ok>()
            .Value.Select(item => item.Content)
            .ShouldBe(["Use a concise verification table."]);
        var foreignScope = scope with { PrincipalId = PrincipalId + 1 };
        (await memoryStore.ListAsync(foreignScope))
            .ShouldBeOfType<Result<IReadOnlyList<AeroAiExplicitMemory>>.Ok>()
            .Value.ShouldBeEmpty();
        (await memoryStore.DeleteAsync(foreignScope, memory.Id))
            .ShouldBeOfType<Result<bool>.Failure>();
        (await memoryStore.DeleteAsync(scope, memory.Id))
            .ShouldBeOfType<Result<bool>.Ok>();
        (await memoryStore.ListAsync(scope))
            .ShouldBeOfType<Result<IReadOnlyList<AeroAiExplicitMemory>>.Ok>()
            .Value.ShouldBeEmpty();

        var publicScope = scope with
        {
            Audience = AeroAiAudience.Public,
            PrincipalKind = AeroAiPrincipalKind.ManagerUser
        };
        (await memoryStore.SaveAsync(
                publicScope,
                new AeroAiExplicitMemoryWrite("Unsafe", "Do not persist this.")))
            .ShouldBeOfType<Result<AeroAiExplicitMemory>.Failure>();
    }

    private static AeroAiConversationStore CreateConversationStore(IDocumentSession session)
        => new(session, NullLogger<AeroAiConversationStore>.Instance);

    private static AeroAiExplicitMemoryStore CreateMemoryStore(IDocumentSession session)
        => new(session, NullLogger<AeroAiExplicitMemoryStore>.Instance);

    private static AeroAiMemoryScope ManagerScope()
        => new(
            TenantId,
            SiteId,
            AeroAiAudience.Manager,
            AeroAiPrincipalKind.ManagerUser,
            PrincipalId,
            "en-US");

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithConfiguration(options => new AiModule().Configure(options));
        await harness.InitializeAsync();
        return harness;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? Exception { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Exception = exception;
        }
    }
}
