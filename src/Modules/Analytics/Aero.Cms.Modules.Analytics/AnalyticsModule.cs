using Aero.Cms.Core.Modules;
using Aero.Cms.Core.Pipelines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Analytics;

public class AnalyticsModule : ModuleBase
{
    public override string Name => "Analytics";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<AnalyticsSettings>().BindConfiguration("AeroCms:Analytics");
        services.AddScoped<IPageReadHook, AnalyticsInjectionHook>();
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
        // No specific builder registration needed for basic read hook if it's resolved via DI
    }
}

public class AnalyticsSettings
{
    public string? FacebookPixelId { get; set; }
    public string? GoogleAnalyticsId { get; set; }
    public string? LinkedInPartnerId { get; set; }
    public string? PosthogApiKey { get; set; }
    public string? PosthogHost { get; set; }
    public string? MicrosoftClarityId { get; set; }

    public bool HasFacebook => !string.IsNullOrWhiteSpace(FacebookPixelId);
    public bool HasGoogle => !string.IsNullOrWhiteSpace(GoogleAnalyticsId);
    public bool HasLinkedIn => !string.IsNullOrWhiteSpace(LinkedInPartnerId);
    public bool HasPosthog => !string.IsNullOrWhiteSpace(PosthogApiKey);
    public bool HasClarity => !string.IsNullOrWhiteSpace(MicrosoftClarityId);
}
