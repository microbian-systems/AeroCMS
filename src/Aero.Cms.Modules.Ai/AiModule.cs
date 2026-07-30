using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Core;
using Aero.Core.Ai;
using Aero.Cms.Modules.Ai.Api;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Cms.Modules.Ai.Knowledge;
using Aero.Cms.Modules.Ai.Memory;
using Aero.Cms.Modules.Ai.Services;
using Aero.Cms.Modules.Ai.Validation;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Modules.RateLimiting;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Core.Content.Search;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai;

/// <summary>
/// Registers the AI content-enhancement and translation module with an AeroCMS host.
/// </summary>
/// <remarks>
/// The module maps an authorization-protected administrative API, registers provider clients and
/// validators, and initializes persisted provider defaults during application startup. Invoking an
/// AI operation can send supplied CMS content and prompts to the selected external provider.
/// </remarks>
[Module(nameof(AiModule))]
public sealed class AiModule : AeroWebModule, IUiModule, IConfigureAeroDB
{
    private bool _useDiskAnn;

        /// <summary>
    /// Gets the module identifier used by the module system.
    /// </summary>
public override string Name => nameof(AiModule);
        /// <summary>
    /// Gets the AeroCMS version advertised for this module.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets the author advertised in module metadata.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets the declared module dependencies.
    /// </summary>
    /// <remarks>
    /// Rate-limiting infrastructure must be registered before the AI module contributes its
    /// manager and streaming policies.
    /// </remarks>
public override IReadOnlyList<string> Dependencies => [nameof(RateLimitingModule)];
        /// <summary>
    /// Gets the categories used to group the module in administrative tooling.
    /// </summary>
public override IReadOnlyList<string> Category => ["admin", "ai"];
        /// <summary>
    /// Gets the search and discovery tags associated with the module.
    /// </summary>
public override IReadOnlyList<string> Tags => ["ai", "manager", "content"];

        /// <summary>
    /// Registers AI settings, validation, prompt construction, provider access, and content services.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="config">
    /// The host configuration, if supplied. The module reads its named rate-limit profiles from it.
    /// </param>
    /// <param name="env">
    /// The host environment, if supplied. This implementation does not vary registration by environment.
    /// </param>
    /// <remarks>
    /// Provider calls use a typed HTTP client with a 120-second transport timeout, disabled redirects,
    /// a ten-connection per-server limit, a five-minute pooled connection lifetime, and the registered
    /// retry handler. The retry handler can repeat connection failures or timeouts, so an invocation
    /// may result in more than one billable provider request if the provider received a failed attempt.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        _useDiskAnn = string.Equals(
            config?["AeroCms:Bootstrap:DatabaseMode"],
            "Server",
            StringComparison.OrdinalIgnoreCase);

        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.AiPublic,
            "AiPublic",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 20,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.AiMember,
            "AiMember",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 60,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.AiManager,
            "AiManager",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 30,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroConcurrencyRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.AiStream,
            "AiStream",
            new AeroConcurrencyRateLimitOptions
            {
                PermitLimit = 4,
                QueueLimit = 0
            });

        services.AddDataProtection();
        services.TryAddSingleton<IAiSecretProtector, DataProtectionAiSecretProtector>();
        services.AddScoped<IAiSettingsStore, AiSettingsStore>();
        services.AddScoped<IAiSettingsProvider, AiSettingsProvider>();
        services.AddScoped<IAiChatClientFactory, TornadoAiChatClientFactory>();
        services.AddScoped<IAiContentEnhancementService, AiContentEnhancementService>();
        services.AddScoped<IAiContentTranslationService, AiContentTranslationService>();
        services.AddScoped<IEnhanceContentPromptBuilder, EnhanceContentPromptBuilder>();
        services.AddScoped<ITranslateDocumentPromptBuilder, TranslateDocumentPromptBuilder>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Ai.EnhanceContentRequest>, EnhanceContentRequestValidator>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Ai.TranslateDocumentRequest>, TranslateDocumentRequestValidator>();
        services.TryAddSingleton<IContentEmbeddingGenerator, UnavailableContentEmbeddingGenerator>();
        services.TryAddSingleton<IAeroDocumentationKnowledgeSource, EmbeddedAeroDocumentationKnowledgeSource>();
        services.AddScoped<IAeroDocumentationKnowledgeSynchronizer, AeroDocumentationKnowledgeSynchronizer>();
        services.AddHostedService<AeroDocumentationKnowledgeSyncHostedService>();
        services.AddScoped<IAeroAiKnowledgeProjectionService, AeroAiKnowledgeProjectionService>();
        services.AddScoped<IAeroAiKnowledgeRetriever, AeroAiKnowledgeRetriever>();
        services.AddScoped<IAeroAiConversationStore, AeroAiConversationStore>();
        services.AddScoped<IAeroAiExplicitMemoryStore, AeroAiExplicitMemoryStore>();
        services.AddTransient<TornadoRetryHandler>();

        // Typed HttpClient for outbound LLM provider calls.
        // Retries only on connection failure / timeout via TornadoRetryHandler.
        services.AddHttpClient<TornadoProviderClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        })
        .AddHttpMessageHandler<TornadoRetryHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            MaxConnectionsPerServer = 10,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });
    }

    /// <summary>Configures the disposable, security-scoped AI knowledge projection.</summary>
    public void Configure(StoreOptions opts)
    {
        opts.Schema.Analyzers.DefineAnalyzer(
            AeroAiKnowledgeConstants.AnalyzerName,
            tokenizers:
            [
                Search.Tokenizer.Blank,
                Search.Tokenizer.Class,
                Search.Tokenizer.Punct
            ],
            filters:
            [
                Search.Filter.Lowercase,
                Search.Filter.Ascii
            ]);

        var chunks = opts.Schema.For<AeroAiKnowledgeChunkDocument>()
            .TableName(Schemas.Tables.AiKnowledgeChunks);
        chunks.Identity(chunk => chunk.Id);
        chunks.Index(chunk => chunk.TenantId);
        chunks.Index(chunk => chunk.SiteId);
        chunks.Index(chunk => chunk.Audience);
        chunks.Index(chunk => chunk.Culture);
        chunks.Index(chunk => chunk.SourceKind);
        chunks.Index(chunk => chunk.SourceId);
        chunks.Index(chunk => new
        {
            chunk.TenantId,
            chunk.SiteId,
            chunk.Audience,
            chunk.Culture
        });
        chunks.Index(chunk => new
        {
            chunk.TenantId,
            chunk.SiteId,
            chunk.SourceKind,
            chunk.SourceId
        });
        chunks.FullTextIndex(
            chunk => chunk.FullText,
            AeroAiKnowledgeConstants.AnalyzerName);

        if (_useDiskAnn)
        {
            chunks.DiskannIndex(
                chunk => chunk.Embedding,
                AeroAiKnowledgeConstants.VectorDimensions,
                distance: Search.Distance.Cosine);
        }
        else
        {
            chunks.HnswIndex(
                chunk => chunk.Embedding,
                AeroAiKnowledgeConstants.VectorDimensions,
                Search.Distance.Cosine);
        }

        var documentationChunks = opts.Schema
            .For<AeroManagerDocumentationChunkDocument>()
            .TableName(Schemas.Tables.AiManagerDocumentationChunks);
        documentationChunks.Identity(chunk => chunk.Id);
        documentationChunks.Index(chunk => chunk.CorpusId);
        documentationChunks.Index(chunk => chunk.SourceId);
        documentationChunks.Index(chunk => chunk.SourceUri);
        documentationChunks.Index(chunk => chunk.Culture);
        documentationChunks.Index(chunk => chunk.ContentHash);
        documentationChunks.FullTextIndex(
            chunk => chunk.FullText,
            AeroAiKnowledgeConstants.AnalyzerName);

        if (_useDiskAnn)
        {
            documentationChunks.DiskannIndex(
                chunk => chunk.Embedding,
                AeroAiKnowledgeConstants.VectorDimensions,
                distance: Search.Distance.Cosine);
        }
        else
        {
            documentationChunks.HnswIndex(
                chunk => chunk.Embedding,
                AeroAiKnowledgeConstants.VectorDimensions,
                Search.Distance.Cosine);
        }

        var documentationCorpusStates = opts.Schema
            .For<AeroManagerDocumentationCorpusStateDocument>()
            .TableName(Schemas.Tables.AiManagerDocumentationCorpusStates);
        documentationCorpusStates.Identity(state => state.Id);
        documentationCorpusStates.UseOptimisticConcurrency = true;
        documentationCorpusStates.UniqueIndex(state => state.CorpusId);

        var conversations = opts.Schema.For<AeroAiConversationDocument>()
            .TableName(Schemas.Tables.AiConversations);
        conversations.Identity(conversation => conversation.Id);
        conversations.Index(conversation => new
        {
            conversation.TenantId,
            conversation.SiteId,
            conversation.Audience,
            conversation.PrincipalKind,
            conversation.PrincipalId
        });

        var messages = opts.Schema.For<AeroAiConversationMessageDocument>()
            .TableName(Schemas.Tables.AiConversationMessages);
        messages.Identity(message => message.Id);
        messages.Index(message => message.ConversationId);
        messages.Index(message => new
        {
            message.TenantId,
            message.SiteId,
            message.Audience,
            message.PrincipalKind,
            message.PrincipalId,
            message.ConversationId
        });
        messages.Index(message => new
        {
            message.ConversationId,
            message.Sequence
        });

        var memories = opts.Schema.For<AeroAiExplicitMemoryDocument>()
            .TableName(Schemas.Tables.AiMemories);
        memories.Identity(memory => memory.Id);
        memories.Index(memory => new
        {
            memory.TenantId,
            memory.SiteId,
            memory.Audience,
            memory.PrincipalKind,
            memory.PrincipalId
        });
    }

    /// <inheritdoc />
    public void Configure(IServiceProvider? services, StoreOptions opts)
        => Configure(opts);

        /// <summary>
    /// Maps the authorization-protected administrative AI endpoints.
    /// </summary>
    /// <param name="builder">The host route builder to update.</param>
    /// <returns>A task that is already complete after endpoint registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAiApi();
        return Task.CompletedTask;
    }

        /// <summary>
    /// Initializes missing persisted AI settings from the host configuration and built-in profiles.
    /// </summary>
    /// <param name="sp">The application service provider used to create an initialization scope.</param>
    /// <returns>A task that completes after default settings have been persisted.</returns>
    /// <remarks>
    /// Initialization can write provider profiles and protected configuration-sourced API keys to the
    /// settings store. Exceptions are not converted to a railway result and propagate to the caller.
    /// </remarks>
public override async Task RunAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var settingsStore = scope.ServiceProvider.GetRequiredService<IAiSettingsStore>();
        await settingsStore.EnsureDefaultsAsync();
    }
}
