using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Budget;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Core;
using Aero.Cms.Modules.AiAssistant.Pipeline;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Modules.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>Registers the provider-backed manager assistant without owning provider credentials.</summary>
[Module(nameof(AiAssistantModule))]
public sealed class AiAssistantModule : AeroWebModule
{
    public override string Name => nameof(AiAssistantModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override string Description => "Bounded provider-backed conversations for the AeroCMS manager.";
    public override IReadOnlyList<string> Dependencies =>
        ["AiModule", "IdentityModule", nameof(RateLimitingModule)];
    public override IReadOnlyList<string> Category => ["admin", "ai"];
    public override IReadOnlyList<string> Tags => ["ai", "assistant", "manager"];

    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        var budgetOptions = services.AddOptions<AeroAiTokenBudgetOptions>();
        if (config is not null)
            budgetOptions.Bind(config.GetSection(AeroAiTokenBudgetOptions.SectionName));
        budgetOptions
            .Validate(
                options => options.WindowSeconds is >= 1 and <= 86_400,
                "AI token budget window must be between 1 second and 1 day.")
            .Validate(
                options => options.TokenLimitPerPartition > 0,
                "AI token budget limit must be positive.")
            .Validate(
                options => options.MaximumReservationTokens is >= 1 and <= 1_000_000,
                "AI token reservation limit is invalid.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAeroAiTokenBudgetCoordinator, AeroAiTokenBudgetCoordinator>();
        services.TryAddSingleton<IAeroCmsAssistantOutputPolicy, AeroCmsAssistantOutputPolicy>();
        services.AddScoped<IAeroAiRequestPipeline, AeroAiRequestPipeline>();
        services.AddScoped<IAeroAiPipelineStage, AeroAiRequestNormalizationStage>();
        services.AddScoped<IAeroAiPipelineStage, AeroAiScopeStage>();
        services.AddScoped<IAeroAiPipelineStage, AeroAiInputSafetyStage>();
        services.AddScoped<IAeroAiPipelineStage, AeroAiTelemetryStage>();
        services.AddScoped<AeroCmsAssistantGroundingService>();
        services.AddScoped<IAeroCmsAssistantService, AeroCmsAssistantService>();
        services.AddScoped<IAeroCmsSiteAssistantService, AeroCmsAssistantService>();
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAeroSiteAssistantEndpoints();
        return Task.CompletedTask;
    }
}
