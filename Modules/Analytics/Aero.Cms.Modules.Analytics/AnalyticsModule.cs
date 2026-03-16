using Aero.Cms.Shared.Modules;
using Aero.Cms.Shared.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Analytics;

public class AnalyticsModule : IModule
{
    public string Name => "Aero.Cms.Analytics";
    public string Version => "1.0.0";
    public string Author => "AeroCMS";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<AnalyticsSettings>().BindConfiguration("AeroCms:Analytics");
        services.AddScoped<IPageReadHook, AnalyticsInjectionHook>();
    }

    public void Configure(IModuleBuilder builder)
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
