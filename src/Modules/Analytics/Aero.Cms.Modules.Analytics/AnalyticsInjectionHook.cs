using System.Text;
using Aero.Cms.Web.Core.Pipelines;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Analytics;


// todo - use view components here. StringBuilder is absolute wrong way to do this
public class AnalyticsInjectionHook(IOptions<AnalyticsSettings> settings) : IPageReadHook
{
    private readonly AnalyticsSettings _settings = settings.Value;

    public int Order => 100; // Run late to inject scripts

    public Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        var sb = new StringBuilder();

        if (_settings.HasGoogle)
        {
            sb.AppendLine($"<!-- Google Analytics -->");
            sb.AppendLine($"<script async src=\"https://www.googletagmanager.com/gtag/js?id={_settings.GoogleAnalyticsId}\"></script>");
            sb.AppendLine("<script>");
            sb.AppendLine("  window.dataLayer = window.dataLayer || [];");
            sb.AppendLine("  function gtag(){dataLayer.push(arguments);}");
            sb.AppendLine("  gtag('js', new Date());");
            sb.AppendLine($"  gtag('config', '{_settings.GoogleAnalyticsId}');");
            sb.AppendLine("</script>");
        }

        if (_settings.HasFacebook)
        {
            sb.AppendLine("<!-- Facebook Pixel -->");
            sb.AppendLine("<script>");
            sb.AppendLine("!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');");
            sb.AppendLine($"fbq('init', '{_settings.FacebookPixelId}');");
            sb.AppendLine("fbq('track', 'PageView');");
            sb.AppendLine("</script>");
        }

        if (_settings.HasLinkedIn)
        {
            sb.AppendLine("<!-- LinkedIn Insight Tag -->");
            sb.AppendLine("<script type=\"text/javascript\">");
            sb.AppendLine($"_linkedin_partner_id = \"{_settings.LinkedInPartnerId}\";");
            sb.AppendLine("window._linkedin_data_partner_ids = window._linkedin_data_partner_ids || [];");
            sb.AppendLine("window._linkedin_data_partner_ids.push(_linkedin_partner_id);");
            sb.AppendLine("</script><script type=\"text/javascript\">");
            sb.AppendLine("(function(l) {");
            sb.AppendLine("if (!l){window.lintrk = function(a,b){window.lintrk.q.push([a,b])};window.lintrk.q=[]}");
            sb.AppendLine("var s = document.getElementsByTagName(\"script\")[0];");
            sb.AppendLine("var b = document.createElement(\"script\");");
            sb.AppendLine("b.type = \"text/javascript\";b.async = true;");
            sb.AppendLine("b.src = \"https://snap.licdn.com/li.lms-analytics/insight.min.js\";");
            sb.AppendLine("s.parentNode.insertBefore(b, s);})(window.lintrk);");
            sb.AppendLine("</script>");
        }

        if (_settings.HasPosthog)
        {
            sb.AppendLine("<!-- Posthog -->");
            sb.AppendLine("<script>");
            sb.AppendLine($"!function(t,e){{var o,n,p,r;e.__SV||(window.posthog=e,e._i=[],e.init=function(i,s,a){{function g(t,e){{var o=e.split(\".\");2==o.length&&(t=t[o[0]],e=o[1]),t[e]=function(){{t.push([e].concat(Array.prototype.slice.call(arguments,0)))}}}}var c=e;for(void 0!==a?c=e[a]=[]:a=\"posthog\",c.people=c.people||[],c.toString=function(t){{var e=\"posthog\";return\"posthog\"!==a&&(e+=\".\"+a),t||(e+=\" (stub)\"),e}},c.people.toString=function(){{return c.toString(1)+\".people (stub)\"}},o=\"capture register register_once unregister opt_out_capturing has_opted_out_capturing set_config reset group alias set_person_properties properties.set properties.set_once edit_person_properties identify first_known_visitor onFeatureFlags onSessionId get_property getSessionId set_config\".split(\" \"),n=0;n<o.length;n++)g(c,o[n]);e._i.push([i,s,a])}},e.__SV=1.0)}}(document,window.posthog||[]);");
            sb.AppendLine($"posthog.init('{_settings.PosthogApiKey}',{{api_host:'{_settings.PosthogHost ?? "https://app.posthog.com"}'}})");
            sb.AppendLine("</script>");
        }

        if (_settings.HasClarity)
        {
            sb.AppendLine("<!-- Microsoft Clarity -->");
            sb.AppendLine("<script type=\"text/javascript\">");
            sb.AppendLine($"(function(c,l,a,r,i,t,y){{c[a]=c[a]||function(){{(c[a].q=c[a].q||[]).push(arguments)}};t=l.createElement(r);t.async=1;t.src=\"https://www.clarity.ms/tag/\"+i;y=l.getElementsByTagName(r)[0];y.parentNode.insertBefore(t,y)}})(window, document, \"clarity\", \"script\", \"{_settings.MicrosoftClarityId}\");");
            sb.AppendLine("</script>");
        }

        if (sb.Length > 0)
        {
            ctx.Metadata["AnalyticsScripts"] = sb.ToString();
        }

        return Task.CompletedTask;
    }
}
