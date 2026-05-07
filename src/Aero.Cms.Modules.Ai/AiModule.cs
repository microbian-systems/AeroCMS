using Aero.Cms.Core;
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

[Module(nameof(AiModule))]
public sealed class AiModule : AeroWebModule, IUiModule
{
    public override string Name => nameof(AiModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["admin", "ai"];
    public override IReadOnlyList<string> Tags => ["ai", "manager", "content"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddDataProtection();
        services.TryAddSingleton<IAiSecretProtector, DataProtectionAiSecretProtector>();
        services.AddScoped<IAiSettingsStore, AiSettingsStore>();
        services.AddScoped<IAiSettingsProvider, AiSettingsProvider>();
        services.AddScoped<IAiChatClientFactory, TornadoAiChatClientFactory>();
        services.AddScoped<IAiContentEnhancementService, AiContentEnhancementService>();
        services.AddScoped<IEnhanceContentPromptBuilder, EnhanceContentPromptBuilder>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Ai.EnhanceContentRequest>, EnhanceContentRequestValidator>();
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAiApi();
        return Task.CompletedTask;
    }

    public override async Task RunAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var settingsStore = scope.ServiceProvider.GetRequiredService<IAiSettingsStore>();
        await settingsStore.EnsureDefaultsAsync();
    }
}

