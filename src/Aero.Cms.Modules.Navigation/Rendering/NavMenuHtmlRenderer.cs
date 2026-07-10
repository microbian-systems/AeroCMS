using System.Text;
using System.Text.Encodings.Web;
using System.Globalization;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Modules.Navigation.Domain;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Navigation.Rendering;

/// <summary>
/// Defines an enumeration for NavMenuRenderMode.
/// </summary>
public enum NavMenuRenderMode
{
    Desktop,
    Tablet,
    Mobile
}

/// <summary>
/// Defines an interface for INavMenuComponentVisitor.
/// </summary>
public interface INavMenuComponentVisitor<out TResult>
{
        /// <summary>
    /// Visit method.
    /// </summary>
TResult Visit(NavLink link);
        /// <summary>
    /// Visit method.
    /// </summary>
TResult Visit(NavMenu menu);
        /// <summary>
    /// Visit method.
    /// </summary>
TResult Visit(NavSearch search);
        /// <summary>
    /// Visit method.
    /// </summary>
TResult Visit(NavHtml html);
        /// <summary>
    /// Visit method.
    /// </summary>
TResult Visit(NavLanguageSelect language);
        /// <summary>
    /// Visit method.
    /// </summary>
TResult Visit(NavAuthButton authButton);
}

/// <summary>
/// Defines an interface for INavMenuHtmlRenderer.
/// </summary>
public interface INavMenuHtmlRenderer
{
        /// <summary>
    /// Render method.
    /// </summary>
IHtmlContent Render(INavMenuComponent component, NavMenuRenderMode mode);
}

/// <summary>
/// Represents a class for NavMenuHtmlRenderer.
/// </summary>
public sealed class NavMenuHtmlRenderer(IHttpContextAccessor httpContextAccessor) : INavMenuHtmlRenderer
{
        /// <summary>
    /// Render method.
    /// </summary>
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

    private sealed class HtmlVisitor(NavMenuRenderMode mode, HttpContext? httpContext) : INavMenuComponentVisitor<IHtmlContent>
    {
        private bool IsMobile => mode == NavMenuRenderMode.Mobile;

                /// <summary>
        /// Visit method.
        /// </summary>
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
        /// Visit method.
        /// </summary>
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
        /// Visit method.
        /// </summary>
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
        /// Visit method.
        /// </summary>
public IHtmlContent Visit(NavHtml html)
            => ShouldRender(html.Visibility) ? new HtmlString(html.Html) : HtmlString.Empty;

                /// <summary>
        /// Visit method.
        /// </summary>
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
        /// Visit method.
        /// </summary>
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

        private static string Encode(string value)
            => HtmlEncoder.Default.Encode(value);

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

        private static string BuildCulturePath(string culture, string? slug)
        {
            var normalizedCulture = NormalizeCultureOrDefault(culture).ToLowerInvariant();
            var normalizedSlug = (slug ?? string.Empty).Trim('/');
            return string.IsNullOrWhiteSpace(normalizedSlug)
                ? $"/{normalizedCulture}"
                : $"/{normalizedCulture}/{normalizedSlug}";
        }

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

        private const string GlobeSvg = "<svg aria-hidden=\"true\" viewBox=\"0 0 24 24\" class=\"h-5 w-5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M2 12h20\"></path><path d=\"M12 2a15.3 15.3 0 0 1 0 20\"></path><path d=\"M12 2a15.3 15.3 0 0 0 0 20\"></path></svg>";
    }
}
