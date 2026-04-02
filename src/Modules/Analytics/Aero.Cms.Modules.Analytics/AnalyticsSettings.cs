namespace Aero.Cms.Modules.Analytics;

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
