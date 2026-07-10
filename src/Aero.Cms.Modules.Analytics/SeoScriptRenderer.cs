using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Analytics;

/// <summary>
/// Defines an interface for ISeoScriptRenderer.
/// </summary>
public interface ISeoScriptRenderer
{
        /// <summary>
    /// RenderAsync method.
    /// </summary>
Task<IHtmlContent> RenderAsync(SeoScriptPlacement placement, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for SeoScriptRenderer.
/// </summary>
public sealed class SeoScriptRenderer(
    IOptions<AnalyticsSettings> settings,
    IAeroSettingActor settingActor) : ISeoScriptRenderer
{
    private readonly AnalyticsSettings _configuredSettings = settings.Value;

        /// <summary>
    /// RenderAsync method.
    /// </summary>
public async Task<IHtmlContent> RenderAsync(SeoScriptPlacement placement, CancellationToken cancellationToken = default)
    {
        var scriptSettings = await LoadScriptSettingsAsync(cancellationToken);
        var builder = new StringBuilder();

        switch (placement)
        {
            case SeoScriptPlacement.Head:
                AppendGoogleAnalytics(builder, scriptSettings);
                AppendPosthog(builder, scriptSettings);
                AppendClarity(builder, scriptSettings);
                break;

            case SeoScriptPlacement.BodyStart:
                AppendFacebookNoscript(builder, scriptSettings);
                AppendLinkedInNoscript(builder, scriptSettings);
                break;

            case SeoScriptPlacement.BodyEnd:
                AppendFacebookPixel(builder, scriptSettings);
                AppendLinkedInInsight(builder, scriptSettings);
                break;
        }

        return builder.Length == 0
            ? HtmlString.Empty
            : new HtmlString(builder.ToString());
    }

    private async Task<AnalyticsSettings> LoadScriptSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await settingActor.GetByCategoryAsync("SEO", cancellationToken);
        return new AnalyticsSettings
        {
            GoogleAnalyticsId = GetSetting(settings, "SEO.GoogleAnalyticsId", _configuredSettings.GoogleAnalyticsId),
            FacebookPixelId = GetSetting(settings, "SEO.FacebookPixelId", _configuredSettings.FacebookPixelId),
            LinkedInPartnerId = GetSetting(settings, "SEO.LinkedInPartnerId", _configuredSettings.LinkedInPartnerId),
            PosthogApiKey = GetSetting(settings, "SEO.PosthogApiKey", _configuredSettings.PosthogApiKey),
            PosthogHost = GetSetting(settings, "SEO.PosthogHost", _configuredSettings.PosthogHost),
            MicrosoftClarityId = GetSetting(settings, "SEO.MicrosoftClarityId", _configuredSettings.MicrosoftClarityId)
        };
    }

    private static string? GetSetting(IEnumerable<SettingDetail> settings, string key, string? fallback)
    {
        var setting = settings.FirstOrDefault(setting => setting.Key == key);
        return setting is null ? fallback : setting.Value;
    }

    private static void AppendGoogleAnalytics(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasGoogle)
        {
            return;
        }

        var id = JavaScriptEncoder.Default.Encode(settings.GoogleAnalyticsId!);
        var srcId = UrlEncoder.Default.Encode(settings.GoogleAnalyticsId!);
        builder.AppendLine(CultureInfo.InvariantCulture, $"<script async src=\"https://www.googletagmanager.com/gtag/js?id={srcId}\"></script>");
        builder.AppendLine("<script>");
        builder.AppendLine("window.dataLayer = window.dataLayer || [];");
        builder.AppendLine("function gtag(){dataLayer.push(arguments);}");
        builder.AppendLine("gtag('js', new Date());");
        builder.AppendLine(CultureInfo.InvariantCulture, $"gtag('config', '{id}');");
        builder.AppendLine("</script>");
    }

    private static void AppendFacebookPixel(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasFacebook)
        {
            return;
        }

        var id = JavaScriptEncoder.Default.Encode(settings.FacebookPixelId!);
        builder.AppendLine("<script>");
        builder.AppendLine("!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');");
        builder.AppendLine(CultureInfo.InvariantCulture, $"fbq('init', '{id}');");
        builder.AppendLine("fbq('track', 'PageView');");
        builder.AppendLine("</script>");
    }

    private static void AppendFacebookNoscript(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasFacebook)
        {
            return;
        }

        var id = UrlEncoder.Default.Encode(settings.FacebookPixelId!);
        builder.AppendLine(CultureInfo.InvariantCulture, $"<noscript><img height=\"1\" width=\"1\" style=\"display:none\" src=\"https://www.facebook.com/tr?id={id}&amp;ev=PageView&amp;noscript=1\" alt=\"\" /></noscript>");
    }

    private static void AppendLinkedInInsight(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasLinkedIn)
        {
            return;
        }

        var id = JavaScriptEncoder.Default.Encode(settings.LinkedInPartnerId!);
        builder.AppendLine("<script>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"_linkedin_partner_id = \"{id}\";");
        builder.AppendLine("window._linkedin_data_partner_ids = window._linkedin_data_partner_ids || [];");
        builder.AppendLine("window._linkedin_data_partner_ids.push(_linkedin_partner_id);");
        builder.AppendLine("(function(l) {");
        builder.AppendLine("if (!l){window.lintrk = function(a,b){window.lintrk.q.push([a,b])};window.lintrk.q=[]}");
        builder.AppendLine("var s = document.getElementsByTagName(\"script\")[0];");
        builder.AppendLine("var b = document.createElement(\"script\");");
        builder.AppendLine("b.type = \"text/javascript\";b.async = true;");
        builder.AppendLine("b.src = \"https://snap.licdn.com/li.lms-analytics/insight.min.js\";");
        builder.AppendLine("s.parentNode.insertBefore(b, s);})(window.lintrk);");
        builder.AppendLine("</script>");
    }

    private static void AppendLinkedInNoscript(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasLinkedIn)
        {
            return;
        }

        var id = UrlEncoder.Default.Encode(settings.LinkedInPartnerId!);
        builder.AppendLine(CultureInfo.InvariantCulture, $"<noscript><img height=\"1\" width=\"1\" style=\"display:none\" alt=\"\" src=\"https://px.ads.linkedin.com/collect/?pid={id}&amp;fmt=gif\" /></noscript>");
    }

    private static void AppendPosthog(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasPosthog)
        {
            return;
        }

        var key = JavaScriptEncoder.Default.Encode(settings.PosthogApiKey!);
        var host = JavaScriptEncoder.Default.Encode(string.IsNullOrWhiteSpace(settings.PosthogHost) ? "https://app.posthog.com" : settings.PosthogHost!);
        builder.AppendLine("<script>");
        builder.AppendLine("!function(t,e){var o,n,p,r;e.__SV||(window.posthog=e,e._i=[],e.init=function(i,s,a){function g(t,e){var o=e.split(\".\");2==o.length&&(t=t[o[0]],e=o[1]),t[e]=function(){t.push([e].concat(Array.prototype.slice.call(arguments,0)))}}var c=e;for(void 0!==a?c=e[a]=[]:a=\"posthog\",c.people=c.people||[],c.toString=function(t){var e=\"posthog\";return\"posthog\"!==a&&(e+=\".\"+a),t||(e+=\" (stub)\"),e},c.people.toString=function(){return c.toString(1)+\".people (stub)\"},o=\"capture register register_once unregister opt_out_capturing has_opted_out_capturing set_config reset group alias set_person_properties properties.set properties.set_once edit_person_properties identify first_known_visitor onFeatureFlags onSessionId get_property getSessionId set_config\".split(\" \"),n=0;n<o.length;n++)g(c,o[n]);e._i.push([i,s,a])},e.__SV=1.0)}(document,window.posthog||[]);");
        builder.AppendLine(CultureInfo.InvariantCulture, $"posthog.init('{key}',{{api_host:'{host}'}});");
        builder.AppendLine("</script>");
    }

    private static void AppendClarity(StringBuilder builder, AnalyticsSettings settings)
    {
        if (!settings.HasClarity)
        {
            return;
        }

        var id = JavaScriptEncoder.Default.Encode(settings.MicrosoftClarityId!);
        builder.AppendLine("<script>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"(function(c,l,a,r,i,t,y){{c[a]=c[a]||function(){{(c[a].q=c[a].q||[]).push(arguments)}};t=l.createElement(r);t.async=1;t.src=\"https://www.clarity.ms/tag/\"+i;y=l.getElementsByTagName(r)[0];y.parentNode.insertBefore(t,y)}})(window, document, \"clarity\", \"script\", \"{id}\");");
        builder.AppendLine("</script>");
    }
}
