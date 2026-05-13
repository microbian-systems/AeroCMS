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

        builder.Append("<div class=\"relative mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8\">");
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

        var socials = snapshot.Sections.OfType<FooterSocialLinks>().OrderBy(x => x.Order).ToList();
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

        builder.Append("</div></div></footer>");
        return new HtmlString(builder.ToString());
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
}
