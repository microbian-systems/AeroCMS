namespace Aero.Cms.Shared.Pages.Manager.SeoScripts;

public static class SeoProviderRegistry
{
    public static IReadOnlyList<SeoProviderDefinition> Definitions { get; } =
    [
        new(
            "google-analytics",
            "Google Analytics",
            "SEO.GoogleAnalyticsId",
            "Measurement ID",
            "Google tag measurement for page views and events."),
        new(
            "facebook-pixel",
            "Facebook Pixel",
            "SEO.FacebookPixelId",
            "Pixel ID",
            "Meta/Facebook page view tracking and conversion attribution."),
        new(
            "linkedin",
            "LinkedIn Insight",
            "SEO.LinkedInPartnerId",
            "Partner ID",
            "LinkedIn insight tag for campaign attribution."),
        new(
            "posthog",
            "PostHog",
            "SEO.PosthogApiKey",
            "Project API Key",
            "PostHog product analytics and event tracking.",
            "SEO.PosthogHost",
            "API Host",
            "https://app.posthog.com"),
        new(
            "microsoft-clarity",
            "Microsoft Clarity",
            "SEO.MicrosoftClarityId",
            "Project ID",
            "Microsoft Clarity session insights and heatmaps.")
    ];

    public static SeoProviderDefinition? Find(string key)
        => Definitions.FirstOrDefault(provider => string.Equals(provider.Key, key, StringComparison.OrdinalIgnoreCase));
}
