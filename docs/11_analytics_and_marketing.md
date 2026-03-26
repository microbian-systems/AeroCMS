# Aero.Cms: Analytics & Marketing Integration

Aero.Cms provides a built-in `AnalyticsModule` that simplifies the integration of popular tracking and marketing scripts. Instead of manually adding scripts to layouts, you can configure these globally via your application settings.

## Supported Providers

The `AnalyticsModule` currently supports the following platforms:
- **Google Analytics (gtag.js)**: Global site tag for Google Analytics tracking.
- **Microsoft Clarity**: User behavior analytics and heatmaps.
- **Facebook Pixel**: Conversion tracking and optimization for Facebook ads.
- **LinkedIn Insight Tag**: Conversion tracking and website demographics for LinkedIn.
- **PostHog**: Product analytics and session recording.

## Configuration

> [!IMPORTANT]
> **Persistence Strategy**: All Aero.Cms settings are stored as documents within **Marten (PostgreSQL)**. Only the database connection string is stored in your `appsettings.{env}.json` files (and is typically encrypted for security).

Analytics are managed via the Admin UI, which persists the following schema to the database:

```json
{
  "FacebookPixelId": "xxxxxxxxxxxxxxx",
  "GoogleAnalyticsId": "G-XXXXXXXXXX",
  "LinkedInPartnerId": "xxxxxxx",
  "PosthogApiKey": "phc_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "PosthogHost": "https://app.posthog.com",
  "MicrosoftClarityId": "xxxxxxxxxx"
}
```

While the settings are stored in the database, you can also override them via the `AeroCms:Analytics` section in your environment variables for CI/CD purposes.

## How it Works

The system uses an `IPageReadHook` (specifically `AnalyticsInjectionHook`) in the page rendering pipeline. 
1. The hook resolves the `AnalyticsSettings` from the configuration.
2. It generates the appropriate `<script>` tags based on the provided IDs.
3. These scripts are injected into the page metadata under the key `AnalyticsScripts`.
4. The CMS layout renders these scripts in either the `<head>` or before the closing `</body>` tag (depending on the implementation of the main `_Layout.cshtml`).

## Adding Custom Tracking

For services not natively supported by the module, you can use the **Raw HTML Block** to inject custom scripts or pixels directly into specific pages.

---

> [!TIP]
> Always ensure you have updated your Privacy Policy and Cookie Consent mechanisms when enabling these tracking services to comply with GDPR/CCPA regulations.
