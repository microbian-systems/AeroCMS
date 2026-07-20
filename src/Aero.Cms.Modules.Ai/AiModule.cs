using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Core;
using Aero.Core.Ai;
using Aero.Cms.Modules.Ai.Api;
using Aero.Cms.Modules.Ai.Configuration;
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
public sealed class AiModule : AeroWebModule, IUiModule
{
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
    /// <remarks>The module currently declares no ordering dependencies.</remarks>
public override IReadOnlyList<string> Dependencies => [];
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
    /// The host configuration, if supplied. This implementation does not read the parameter directly.
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
