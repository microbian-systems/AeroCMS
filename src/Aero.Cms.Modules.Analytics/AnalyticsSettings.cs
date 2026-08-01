namespace Aero.Cms.Modules.Analytics;

/// <summary>
/// Configures identifiers and endpoints used to render third-party analytics tags.
/// </summary>
/// <remarks>
/// Non-empty values enable the corresponding tag. This object contains only configuration values;
/// it does not provide consent management, user targeting, tenant scoping, or event storage.
/// </remarks>
public class AnalyticsSettings
{
        /// <summary>
    /// Gets or sets the Facebook Pixel Id.
    /// </summary>
public string? FacebookPixelId { get; set; }
        /// <summary>
    /// Gets or sets the Google Analytics Id.
    /// </summary>
public string? GoogleAnalyticsId { get; set; }
        /// <summary>
    /// Gets or sets the Linked In Partner Id.
    /// </summary>
public string? LinkedInPartnerId { get; set; }
    /// <summary>
    /// Gets or sets the PostHog project API key emitted into client-side bootstrap markup.
    /// </summary>
public string? PosthogApiKey { get; set; }
    /// <summary>
    /// Gets or sets the PostHog API host; rendering defaults to <c>https://app.posthog.com</c> when it is blank.
    /// </summary>
public string? PosthogHost { get; set; }
        /// <summary>
    /// Gets or sets the Microsoft Clarity Id.
    /// </summary>
public string? MicrosoftClarityId { get; set; }

        /// <summary>
    /// Gets or sets the Has Facebook.
    /// </summary>
public bool HasFacebook => !string.IsNullOrWhiteSpace(FacebookPixelId);
        /// <summary>
    /// Gets or sets the Has Google.
    /// </summary>
public bool HasGoogle => !string.IsNullOrWhiteSpace(GoogleAnalyticsId);
        /// <summary>
    /// Gets or sets the Has Linked In.
    /// </summary>
public bool HasLinkedIn => !string.IsNullOrWhiteSpace(LinkedInPartnerId);
        /// <summary>
    /// Gets or sets the Has Posthog.
    /// </summary>
public bool HasPosthog => !string.IsNullOrWhiteSpace(PosthogApiKey);
        /// <summary>
    /// Gets or sets the Has Clarity.
    /// </summary>
public bool HasClarity => !string.IsNullOrWhiteSpace(MicrosoftClarityId);
}
