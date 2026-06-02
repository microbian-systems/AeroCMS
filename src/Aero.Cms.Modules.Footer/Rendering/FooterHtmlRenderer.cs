using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Aero.Cms.Modules.Footer.Domain;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Modules.Footer.Rendering;

public interface IFooterHtmlRenderer
{
    IHtmlContent Render(FooterSnapshot? snapshot);
}

public sealed class FooterHtmlRenderer : IFooterHtmlRenderer
{
    public IHtmlContent Render(FooterSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return HtmlString.Empty;
        }

        var builder = new StringBuilder();
        var backgroundStyle = BuildBackgroundStyle(snapshot.Style);
        builder.Append(CultureInfo.InvariantCulture, $"<footer class=\"relative mt-auto overflow-hidden bg-slate-950 text-slate-100\"{backgroundStyle}>");
        if (!string.IsNullOrWhiteSpace(snapshot.Style.BackgroundImageUrl))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"absolute inset-0 bg-slate-950\" style=\"opacity:{snapshot.Style.OverlayOpacity.ToString(CultureInfo.InvariantCulture)}\"></div>");
        }

        if (snapshot.Rows.Count > 0)
        {
            builder.Append("<style>.aero-footer-canvas-column{grid-column:span 12/span 12}@media (min-width:768px){.aero-footer-canvas-column{grid-column:span var(--aero-tablet-span)/span var(--aero-tablet-span)}}@media (min-width:1024px){.aero-footer-canvas-column{grid-column:span var(--aero-desktop-span)/span var(--aero-desktop-span)}}</style>");
        }

        builder.Append("<div class=\"relative mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8\">");
        if (snapshot.Rows.Count > 0)
        {
            RenderRows(builder, snapshot);
            RenderFooterBottom(builder, snapshot);
            builder.Append("</div></footer>");
            return new HtmlString(builder.ToString());
        }

        builder.Append("<div class=\"grid gap-10 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,2fr)]\">");
        builder.Append("<div class=\"space-y-4\">");
        RenderBrand(builder, snapshot);
        foreach (var text in snapshot.Sections.OfType<FooterTextBlock>().OrderBy(x => x.Order))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<p class=\"max-w-sm text-sm leading-6 text-slate-300\">{Encode(text.Text)}</p>");
        }

        builder.Append("</div>");
        builder.Append("<div class=\"grid gap-8 sm:grid-cols-2 lg:grid-cols-3\">");
        foreach (var group in snapshot.Sections.OfType<FooterLinkGroup>().OrderBy(x => x.Order))
        {
            RenderLinkGroup(builder, group);
        }

        builder.Append("</div></div>");

        RenderFooterBottom(builder, snapshot);
        builder.Append("</div></footer>");
        return new HtmlString(builder.ToString());
    }

    private static void RenderRows(StringBuilder builder, FooterSnapshot snapshot)
    {
        builder.Append("<div class=\"space-y-8\">");
        foreach (var row in snapshot.Rows.OrderBy(x => x.Order))
        {
            builder.Append("<div class=\"grid grid-cols-12 gap-6\">");
            foreach (var column in row.Columns.OrderBy(x => x.Order))
            {
                var style = ColumnStyle(column);
                builder.Append(CultureInfo.InvariantCulture, $"<div class=\"aero-footer-canvas-column space-y-4\" style=\"{style}\">");
                foreach (var block in column.Blocks.OrderBy(x => x.Order))
                {
                    RenderComponent(builder, block.Component);
                }

                builder.Append("</div>");
            }

            builder.Append("</div>");
        }

        builder.Append("</div>");
    }

    private static void RenderComponent(StringBuilder builder, IFooterComponent component)
    {
        switch (component)
        {
            case FooterLinkGroup group:
                RenderLinkGroup(builder, group);
                break;

            case FooterTextBlock text:
                builder.Append(CultureInfo.InvariantCulture, $"<p class=\"max-w-sm text-sm leading-6 text-slate-300\">{Encode(text.Text)}</p>");
                break;

            case FooterSocialLinks social:
                if (social.Links.Count > 0)
                {
                    builder.Append("<div class=\"flex flex-wrap items-center gap-4\">");
                    foreach (var link in social.Links)
                    {
                        RenderAnchor(builder, link.Platform, link.Href, true, "text-sm font-semibold text-slate-300 hover:text-white");
                    }

                    builder.Append("</div>");
                }

                break;

            case FooterNewsletterSignup newsletter:
                builder.Append(CultureInfo.InvariantCulture, $"<form action=\"{Encode(newsletter.EndpointKey)}\" method=\"post\" class=\"flex max-w-sm flex-col gap-2 sm:flex-row\">");
                builder.Append(CultureInfo.InvariantCulture, $"<input type=\"email\" name=\"email\" placeholder=\"{Encode(newsletter.Placeholder)}\" class=\"min-w-0 flex-1 rounded border border-white/10 bg-white/10 px-3 py-2 text-sm text-white placeholder:text-slate-400\" />");
                builder.Append(CultureInfo.InvariantCulture, $"<button type=\"submit\" class=\"rounded bg-white px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-slate-200\">{Encode(newsletter.ButtonLabel)}</button>");
                builder.Append("</form>");
                break;

            case FooterSearch search:
                builder.Append(CultureInfo.InvariantCulture, $"<form action=\"{Encode(search.SearchAction)}\" method=\"get\" class=\"max-w-sm\"><input name=\"q\" placeholder=\"{Encode(search.Placeholder)}\" class=\"w-full rounded border border-white/10 bg-white/10 px-3 py-2 text-sm text-white placeholder:text-slate-400\" /></form>");
                break;

            case FooterSpacer spacer:
                var height = spacer.SizeToken?.ToLowerInvariant() switch
                {
                    "sm" => "h-3",
                    "lg" => "h-10",
                    _ => "h-6"
                };
                builder.Append(CultureInfo.InvariantCulture, $"<div class=\"{height}\" aria-hidden=\"true\"></div>");
                break;
        }
    }

    private static void RenderFooterBottom(StringBuilder builder, FooterSnapshot snapshot)
    {
        var socials = snapshot.Components.OfType<FooterSocialLinks>().OrderBy(x => x.Order).ToList();
        builder.Append("<div class=\"mt-10 flex flex-col gap-4 border-t border-white/10 pt-6 sm:flex-row sm:items-center sm:justify-between\">");
        builder.Append(CultureInfo.InvariantCulture, $"<p class=\"text-sm text-slate-400\">{BuildCopyright(snapshot)}</p>");
        if (snapshot.Legal.LegalLinks.Count > 0 || socials.Count > 0)
        {
            builder.Append("<div class=\"flex flex-wrap items-center gap-4\">");
            foreach (var link in snapshot.Legal.LegalLinks)
            {
                RenderAnchor(builder, link.Label, link.Href, link.OpenInNewTab, "text-sm text-slate-300 hover:text-white");
            }

            foreach (var social in socials.SelectMany(x => x.Links))
            {
                RenderAnchor(builder, social.Platform, social.Href, true, "text-sm font-semibold text-slate-300 hover:text-white");
            }

            builder.Append("</div>");
        }

        builder.Append("</div>");
    }

    private static string BuildBackgroundStyle(FooterStyleSettings style)
    {
        if (string.IsNullOrWhiteSpace(style.BackgroundImageUrl))
        {
            return string.Empty;
        }

        var mode = style.BackgroundImageMode is "contain" or "repeat" ? style.BackgroundImageMode : "cover";
        var repeat = mode == "repeat" ? "repeat" : "no-repeat";
        var size = mode == "repeat" ? "auto" : mode;
        return $" style=\"background-image:url('{Encode(style.BackgroundImageUrl)}');background-size:{size};background-position:center;background-repeat:{repeat};\"";
    }

    private static void RenderBrand(StringBuilder builder, FooterSnapshot snapshot)
    {
        builder.Append("<a href=\"/\" class=\"inline-flex items-center gap-3\">");
        if (!string.IsNullOrWhiteSpace(snapshot.Brand.LogoUrl))
        {
            var alt = string.IsNullOrWhiteSpace(snapshot.Brand.LogoAltText)
                ? $"{snapshot.Brand.CompanyName} logo"
                : snapshot.Brand.LogoAltText;
            builder.Append(CultureInfo.InvariantCulture, $"<img src=\"{Encode(snapshot.Brand.LogoUrl)}\" alt=\"{Encode(alt)}\" class=\"h-10 max-w-48 object-contain\" />");
        }
        else
        {
            builder.Append("<span class=\"flex h-10 w-10 items-center justify-center rounded bg-white text-sm font-black text-slate-950\">A</span>");
        }

        builder.Append(CultureInfo.InvariantCulture, $"<span class=\"text-lg font-bold tracking-tight text-white\">{Encode(snapshot.Brand.CompanyName)}</span>");
        builder.Append("</a>");
        if (!string.IsNullOrWhiteSpace(snapshot.Brand.Tagline))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<p class=\"max-w-sm text-sm leading-6 text-slate-300\">{Encode(snapshot.Brand.Tagline)}</p>");
        }
    }

    private static void RenderLinkGroup(StringBuilder builder, FooterLinkGroup group)
    {
        builder.Append("<nav aria-label=\"Footer\" class=\"space-y-3\">");
        builder.Append(CultureInfo.InvariantCulture, $"<h2 class=\"text-sm font-semibold uppercase tracking-wider text-white\">{Encode(group.Title)}</h2>");
        builder.Append("<ul class=\"space-y-2\">");
        foreach (var link in group.Links)
        {
            builder.Append("<li>");
            RenderAnchor(builder, link.Label, link.Href, link.OpenInNewTab, "text-sm text-slate-300 hover:text-white");
            builder.Append("</li>");
        }

        builder.Append("</ul></nav>");
    }

    private static void RenderAnchor(StringBuilder builder, string label, string href, bool openInNewTab, string css)
    {
        var target = openInNewTab ? " target=\"_blank\" rel=\"noopener noreferrer\"" : string.Empty;
        builder.Append(CultureInfo.InvariantCulture, $"<a href=\"{Encode(href)}\" class=\"{css}\"{target}>{Encode(label)}</a>");
    }

    private static string BuildCopyright(FooterSnapshot snapshot)
    {
        var text = string.IsNullOrWhiteSpace(snapshot.Legal.CopyrightText)
            ? $"{snapshot.Brand.CompanyName}. All rights reserved."
            : snapshot.Legal.CopyrightText;

        return snapshot.Legal.AutoAppendCurrentYear
            ? $"© {DateTime.UtcNow.Year} {Encode(text)}"
            : Encode(text);
    }

    private static string Encode(string? value)
        => HtmlEncoder.Default.Encode(value ?? string.Empty);

    private static string ColumnStyle(FooterCanvasColumn column)
        => $"--aero-tablet-span:{Math.Clamp(column.TabletSpan, 1, 12)};--aero-desktop-span:{Math.Clamp(column.DesktopSpan, 1, 12)};";
}
