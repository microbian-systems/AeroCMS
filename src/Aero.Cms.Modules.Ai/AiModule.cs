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
/// Represents a class for AiModule.
/// </summary>
[Module(nameof(AiModule))]
public sealed class AiModule : AeroWebModule, IUiModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(AiModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["admin", "ai"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["ai", "manager", "content"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
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
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAiApi();
        return Task.CompletedTask;
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override async Task RunAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var settingsStore = scope.ServiceProvider.GetRequiredService<IAiSettingsStore>();
        await settingsStore.EnsureDefaultsAsync();
    }
}
