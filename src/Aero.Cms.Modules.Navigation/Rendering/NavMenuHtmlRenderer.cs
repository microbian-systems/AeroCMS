using System.Text;
using System.Text.Encodings.Web;
using System.Globalization;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Navigation.Domain;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Navigation.Rendering;

/// <summary>
/// Selects responsive navigation markup and CSS classes.
/// </summary>
public enum NavMenuRenderMode
{
    /// <summary>Renders the desktop presentation.</summary>
    Desktop,
    /// <summary>Renders the desktop-class presentation at tablet size.</summary>
    Tablet,
    /// <summary>Renders the stacked mobile presentation.</summary>
    Mobile
}

/// <summary>
/// Defines rendering operations for every supported navigation component type.
/// </summary>
/// <typeparam name="TResult">The covariant result produced for each component.</typeparam>
public interface INavMenuComponentVisitor<out TResult>
{
    /// <summary>Visits a navigation link.</summary>
    /// <param name="link">The link component.</param>
    /// <returns>The visitor result.</returns>
TResult Visit(NavLink link);
    /// <summary>Visits a nested menu.</summary>
    /// <param name="menu">The menu component.</param>
    /// <returns>The visitor result.</returns>
TResult Visit(NavMenu menu);
    /// <summary>Visits a search form.</summary>
    /// <param name="search">The search component.</param>
    /// <returns>The visitor result.</returns>
TResult Visit(NavSearch search);
    /// <summary>Visits a trusted custom-HTML component.</summary>
    /// <param name="html">The HTML component.</param>
    /// <returns>The visitor result.</returns>
TResult Visit(NavHtml html);
    /// <summary>Visits a culture selector.</summary>
    /// <param name="language">The language component.</param>
    /// <returns>The visitor result.</returns>
TResult Visit(NavLanguageSelect language);
    /// <summary>Visits an authentication action.</summary>
    /// <param name="authButton">The authentication component.</param>
    /// <returns>The visitor result.</returns>
TResult Visit(NavAuthButton authButton);
}

/// <summary>
/// Renders supported navigation components as HTML for a responsive presentation.
/// </summary>
public interface INavMenuHtmlRenderer
{
    /// <summary>
    /// Renders a component while applying its authentication visibility.
    /// </summary>
    /// <param name="component">The component to render.</param>
    /// <param name="mode">The responsive presentation mode.</param>
    /// <returns>Encoded component markup, raw trusted markup for <see cref="NavHtml"/>, or empty content for unsupported or hidden components.</returns>
IHtmlContent Render(INavMenuComponent component, NavMenuRenderMode mode);
}

/// <summary>
/// Implements HTML rendering with request-aware authentication and culture-switch links.
/// </summary>
/// <remarks>
/// Labels, URLs, actions, placeholders, and targets are HTML-encoded. <see cref="NavHtml.Html"/>
/// is intentionally emitted verbatim and must originate from a trusted, sanitized administrative boundary.
/// </remarks>
public sealed class NavMenuHtmlRenderer(IHttpContextAccessor httpContextAccessor) : INavMenuHtmlRenderer
{
    /// <inheritdoc />
public IHtmlContent Render(INavMenuComponent component, NavMenuRenderMode mode)
    {
        var visitor = new HtmlVisitor(mode, httpContextAccessor.HttpContext);
        return component switch
        {
            NavLink link => visitor.Visit(link),
            NavMenu menu => visitor.Visit(menu),
            NavSearch search => visitor.Visit(search),
            NavHtml html => visitor.Visit(html),
            NavLanguageSelect language => visitor.Visit(language),
            NavAuthButton authButton => visitor.Visit(authButton),
            _ => HtmlString.Empty
        };
    }

    /// <summary>
    /// Renders components using the selected responsive mode and optional active HTTP request.
    /// </summary>
    /// <param name="mode">The responsive presentation mode.</param>
    /// <param name="httpContext">The request context used for identity, site cultures, and path base.</param>
    private sealed class HtmlVisitor(NavMenuRenderMode mode, HttpContext? httpContext) : INavMenuComponentVisitor<IHtmlContent>
    {
        /// <summary>
        /// Gets whether the stacked mobile classes should be used.
        /// </summary>
        private bool IsMobile => mode == NavMenuRenderMode.Mobile;

        /// <summary>
        /// Renders an encoded anchor and adds <c>noopener noreferrer</c> for new-tab links.
        /// </summary>
        /// <param name="link">The link component.</param>
        /// <returns>The anchor or empty content when hidden for the current principal.</returns>
public IHtmlContent Visit(NavLink link)
        {
            if (!ShouldRender(link.Visibility))
            {
                return HtmlString.Empty;
            }

            var css = IsMobile
                ? "block px-4 py-4 rounded-2xl text-lg font-bold text-slate-700 hover:bg-indigo-50 hover:text-indigo-600 transition-all duration-200"
                : "px-4 py-2 rounded-lg text-sm font-semibold text-slate-600 hover:text-indigo-600 hover:bg-slate-50 transition-all duration-200";
            var targetValue = string.IsNullOrWhiteSpace(link.Target)
                ? (link.OpenInNewTab ? "_blank" : "_self")
                : link.Target;
            var target = targetValue == "_self" ? string.Empty : $" target=\"{Encode(targetValue)}\"";
            var rel = targetValue == "_blank" ? " rel=\"noopener noreferrer\"" : string.Empty;

            return new HtmlString(
                $"<a href=\"{Encode(link.Href)}\" class=\"{css}\"{target}{rel}>{Encode(link.Label)}</a>");
        }

        /// <summary>
        /// Renders a dropdown/group and recursively renders supported child components.
        /// </summary>
        /// <param name="menu">The nested menu.</param>
        /// <returns>The group markup or empty content when hidden for the current principal.</returns>
public IHtmlContent Visit(NavMenu menu)
        {
            if (!ShouldRender(menu.Visibility))
            {
                return HtmlString.Empty;
            }

            var builder = new StringBuilder();
            var wrapperClass = IsMobile ? "space-y-2" : "relative group";
            var labelClass = IsMobile
                ? "block px-4 py-4 rounded-2xl text-lg font-bold text-slate-700"
                : "px-4 py-2 rounded-lg text-sm font-semibold text-slate-600";
            var childWrapperClass = IsMobile
                ? "ml-4 border-l border-slate-200 pl-2 space-y-2"
                : "absolute left-0 top-full hidden min-w-48 rounded-lg border border-slate-200 bg-white p-2 shadow-xl group-hover:block";

            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"{wrapperClass}\">");
            builder.Append(CultureInfo.InvariantCulture, $"<span class=\"{labelClass}\">{Encode(menu.Label)}</span>");
            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"{childWrapperClass}\">");
            foreach (var child in menu.Children)
            {
                builder.Append(RenderToString(child));
            }

            builder.Append("</div></div>");
            return new HtmlString(builder.ToString());
        }

        /// <summary>
        /// Renders an encoded GET form whose query input is named <c>q</c>.
        /// </summary>
        /// <param name="search">The search component.</param>
        /// <returns>The form markup or empty content when hidden for the current principal.</returns>
public IHtmlContent Visit(NavSearch search)
        {
            if (!ShouldRender(search.Visibility))
            {
                return HtmlString.Empty;
            }

            var inputCss = IsMobile
                ? "block w-full rounded-xl border border-slate-200 px-4 py-3 text-base"
                : "rounded-lg border border-slate-200 px-3 py-2 text-sm";
            var formCss = IsMobile
                ? "flex w-full items-center gap-2 px-4 py-2"
                : "flex items-center gap-2";
            var buttonCss = IsMobile
                ? "rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white"
                : "rounded-lg bg-indigo-600 px-3 py-2 text-sm font-semibold text-white hover:bg-indigo-700";

            return new HtmlString(
                $"<form action=\"{Encode(search.SearchAction)}\" method=\"get\" class=\"{formCss}\"><input name=\"q\" class=\"{inputCss}\" placeholder=\"{Encode(search.Placeholder)}\" /><button type=\"submit\" class=\"{buttonCss}\">{Encode(search.ButtonLabel)}</button></form>");
        }

        /// <summary>
        /// Emits trusted custom markup without encoding.
        /// </summary>
        /// <param name="html">The trusted HTML component.</param>
        /// <returns>The raw markup or empty content when hidden for the current principal.</returns>
public IHtmlContent Visit(NavHtml html)
            => ShouldRender(html.Visibility) ? new HtmlString(html.Html) : HtmlString.Empty;

        /// <summary>
        /// Renders culture-switch links for the active site's supported cultures.
        /// </summary>
        /// <param name="language">The language selector component.</param>
        /// <returns>The selector markup or empty content when hidden for the current principal.</returns>
        /// <remarks>
        /// Each link preserves the current route after removing an already-supported leading
        /// culture segment. Without an HTTP context, the ambient UI culture is the only option.
        /// </remarks>
public IHtmlContent Visit(NavLanguageSelect language)
        {
            if (!ShouldRender(language.Visibility))
            {
                return HtmlString.Empty;
            }

            var currentCulture = CultureInfo.CurrentUICulture.Name;
            var dropdownCss = IsMobile
                ? "mt-2 space-y-1 rounded-xl border border-slate-200 bg-white p-2"
                : "absolute right-0 top-full z-50 mt-2 hidden min-w-40 rounded-lg border border-slate-200 bg-white p-2 shadow-xl group-open:block";
            var triggerCss = IsMobile
                ? "flex w-full items-center gap-3 rounded-2xl px-4 py-4 text-lg font-bold text-slate-700"
                : "flex h-10 w-10 cursor-pointer list-none items-center justify-center rounded-lg text-slate-600 hover:bg-slate-50 hover:text-indigo-600";

            var options = BuildCultureOptions(currentCulture, httpContext);
            var builder = new StringBuilder();
            builder.Append(IsMobile
                ? "<details class=\"px-0\">"
                : "<details class=\"relative group\">");
            builder.Append(CultureInfo.InvariantCulture, $"<summary class=\"{triggerCss}\" aria-label=\"{Encode(language.Label)}\">");
            builder.Append(GlobeSvg);
            if (IsMobile)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<span>{Encode(language.Label)}</span>");
            }

            builder.Append("</summary>");
            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"{dropdownCss}\">");
            foreach (var option in options)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<a class=\"block rounded px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-indigo-50 hover:text-indigo-600\" href=\"{Encode(option.Href)}\">{Encode(option.Label)}</a>");
            }

            builder.Append("</div></details>");
            return new HtmlString(builder.ToString());
        }

        /// <summary>
        /// Renders an encoded authentication-action anchor using primary or secondary classes.
        /// </summary>
        /// <param name="authButton">The authentication action.</param>
        /// <returns>The anchor or empty content when hidden for the current principal.</returns>
public IHtmlContent Visit(NavAuthButton authButton)
        {
            if (!ShouldRender(authButton.Visibility))
            {
                return HtmlString.Empty;
            }

            var primary = authButton.ButtonStyle.Equals("Primary", StringComparison.OrdinalIgnoreCase);
            var css = primary
                ? IsMobile
                    ? "block rounded-2xl bg-indigo-600 px-4 py-4 text-center text-lg font-bold text-white"
                    : "rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700"
                : IsMobile
                    ? "block rounded-2xl px-4 py-4 text-center text-lg font-bold text-slate-700 hover:bg-indigo-50 hover:text-indigo-600"
                    : "rounded-lg px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50 hover:text-indigo-600";

            return new HtmlString($"<a href=\"{Encode(authButton.Href)}\" class=\"{css}\">{Encode(authButton.Label)}</a>");
        }

        /// <summary>
        /// Renders a nested component to a string without double-encoding its generated markup.
        /// </summary>
        /// <param name="component">The child component.</param>
        /// <returns>The generated markup, or an empty string for unsupported components.</returns>
        private string RenderToString(INavMenuComponent component)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var rendered = component switch
            {
                NavLink link => Visit(link),
                NavMenu menu => Visit(menu),
                NavSearch search => Visit(search),
                NavHtml html => Visit(html),
                NavLanguageSelect language => Visit(language),
                NavAuthButton authButton => Visit(authButton),
                _ => HtmlString.Empty
            };
            rendered.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }

        /// <summary>
        /// HTML-encodes untrusted text and attribute values.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        private static string Encode(string value)
            => HtmlEncoder.Default.Encode(value);

        /// <summary>
        /// Evaluates authentication visibility against the active request principal.
        /// </summary>
        /// <param name="visibility">The component visibility rule.</param>
        /// <returns>Whether the component should be included.</returns>
        /// <remarks>A missing HTTP context is treated as anonymous.</remarks>
        private bool ShouldRender(NavAuthVisibility visibility)
        {
            var isAuthenticated = httpContext?.User?.Identity?.IsAuthenticated == true;
            return visibility switch
            {
                NavAuthVisibility.AnonymousOnly => !isAuthenticated,
                NavAuthVisibility.AuthenticatedOnly => isAuthenticated,
                _ => true
            };
        }

        /// <summary>
        /// Builds native-language labels and culture-prefixed links for the current route.
        /// </summary>
        /// <param name="currentCulture">The ambient UI culture used when site metadata is unavailable.</param>
        /// <param name="httpContext">The request containing the site slice, path, and path base.</param>
        /// <returns>One option per normalized supported culture.</returns>
        private static IReadOnlyList<(string Label, string Href)> BuildCultureOptions(string currentCulture, HttpContext? httpContext)
        {
            var site = httpContext?.Features.Get<IAeroSiteSlice>();
            var defaultCulture = site?.DefaultCulture ?? currentCulture;
            var cultures = NormalizeSupportedCultures(site?.SupportedCultures, defaultCulture);
            var slug = StripLeadingCulture(httpContext?.Request.Path.Value, cultures);
            return cultures
                .Select(culture =>
                {
                    var label = FormatCulture(culture);
                    var href = httpContext is null
                        ? BuildCulturePath(culture, slug)
                        : httpContext.Request.PathBase + BuildCulturePath(culture, slug);
                    return (label, href);
                })
                .ToList();
        }

        /// <summary>
        /// Canonicalizes and de-duplicates site cultures while ensuring the default is present.
        /// </summary>
        /// <param name="cultures">The configured supported cultures.</param>
        /// <param name="defaultCulture">The culture inserted when absent.</param>
        /// <returns>The normalized culture list.</returns>
        private static IReadOnlyList<string> NormalizeSupportedCultures(IEnumerable<string>? cultures, string defaultCulture)
        {
            var normalizedDefault = NormalizeCultureOrDefault(defaultCulture);
            var normalized = (cultures ?? [])
                .Select(culture => NormalizeCultureOrDefault(culture, normalizedDefault))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalized.Contains(normalizedDefault, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Insert(0, normalizedDefault);
            }

            return normalized;
        }

        /// <summary>
        /// Removes a leading route segment only when it matches a supported culture.
        /// </summary>
        /// <param name="path">The current request path.</param>
        /// <param name="cultures">The normalized supported cultures.</param>
        /// <returns>The route remainder without leading or trailing slashes.</returns>
        private static string StripLeadingCulture(string? path, IEnumerable<string> cultures)
        {
            var value = (path ?? string.Empty).Trim('/');
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var segments = value.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            var first = NormalizeCultureOrDefault(segments[0], string.Empty);
            return cultures.Contains(first, StringComparer.OrdinalIgnoreCase)
                ? segments.Length > 1 ? segments[1] : string.Empty
                : value;
        }

        /// <summary>
        /// Creates a lower-case culture-prefixed application path.
        /// </summary>
        /// <param name="culture">The culture prefix.</param>
        /// <param name="slug">The optional route remainder.</param>
        /// <returns>A root-relative culture path.</returns>
        private static string BuildCulturePath(string culture, string? slug)
        {
            var normalizedCulture = NormalizeCultureOrDefault(culture).ToLowerInvariant();
            var normalizedSlug = (slug ?? string.Empty).Trim('/');
            return string.IsNullOrWhiteSpace(normalizedSlug)
                ? $"/{normalizedCulture}"
                : $"/{normalizedCulture}/{normalizedSlug}";
        }

        /// <summary>
        /// Canonicalizes a culture name or returns the supplied fallback.
        /// </summary>
        /// <param name="culture">The candidate culture.</param>
        /// <param name="fallback">The value returned for blank or invalid input.</param>
        /// <returns>The canonical culture name or fallback.</returns>
        private static string NormalizeCultureOrDefault(string? culture, string fallback = "en-US")
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return fallback;
            }

            try
            {
                return CultureInfo.GetCultureInfo(culture.Trim()).Name;
            }
            catch (CultureNotFoundException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// Formats a culture using its native display name.
        /// </summary>
        /// <param name="culture">The canonical culture name.</param>
        /// <returns>The native name, or the original value when it is invalid.</returns>
        private static string FormatCulture(string culture)
        {
            try
            {
                var info = CultureInfo.GetCultureInfo(culture);
                return info.NativeName;
            }
            catch (CultureNotFoundException)
            {
                return culture;
            }
        }

        /// <summary>
        /// Gets the decorative globe icon embedded in the culture selector.
        /// </summary>
        private const string GlobeSvg = "<svg aria-hidden=\"true\" viewBox=\"0 0 24 24\" class=\"h-5 w-5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M2 12h20\"></path><path d=\"M12 2a15.3 15.3 0 0 1 0 20\"></path><path d=\"M12 2a15.3 15.3 0 0 0 0 20\"></path></svg>";
    }
}
