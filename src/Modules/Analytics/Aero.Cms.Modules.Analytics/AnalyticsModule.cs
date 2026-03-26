using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Aero.Cms.Web.Core.Pipelines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Analytics;

public class AnalyticsModule : AeroModuleBase
{
    public override string Name => nameof(AnalyticsModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Marketing", "Tracking"];
    public override IReadOnlyList<string> Tags => ["analytics", "tracking", "metrics"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null, IHostEnvironment env = null)
    {
        services.AddOptions<AnalyticsSettings>().BindConfiguration("AeroCms:Analytics");
        services.AddScoped<IPageReadHook, AnalyticsInjectionHook>();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
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
