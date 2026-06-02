using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Footer.Domain;

public sealed record FooterSnapshot
{
    public FooterBrandSettings Brand { get; init; } = new();
    public FooterStyleSettings Style { get; init; } = FooterStyleSettings.Default;
    public FooterResponsiveSettings Responsive { get; init; } = FooterResponsiveSettings.Default;
    public FooterLegalSettings Legal { get; init; } = FooterLegalSettings.Default;
    public List<FooterCanvasRow> Rows { get; init; } = [];
    public List<IFooterComponent> Sections { get; init; } = [];

    [JsonIgnore]
    public IEnumerable<IFooterComponent> Components => Rows.Count > 0
        ? Rows.OrderBy(row => row.Order)
            .SelectMany(row => row.Columns.OrderBy(column => column.Order))
            .SelectMany(column => column.Blocks.OrderBy(block => block.Order).Select(block => block.Component))
        : Sections.OrderBy(x => x.Order);

    public static FooterSnapshot Empty { get; } = new();

    public void Validate()
    {
        Brand.Validate();
        Style.Validate();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in Components)
        {
            ValidateComponent(component, keys);
        }
    }

    private static void ValidateComponent(IFooterComponent component, HashSet<string> keys)
    {
        if (string.IsNullOrWhiteSpace(component.Key))
        {
            throw new InvalidOperationException("Footer component key is required.");
        }

        if (!keys.Add(component.Key))
        {
            throw new InvalidOperationException($"Duplicate footer component key '{component.Key}'.");
        }

        switch (component)
        {
            case FooterLinkGroup group:
                if (string.IsNullOrWhiteSpace(group.Title))
                    throw new InvalidOperationException("Footer link group title is required.");

                foreach (var link in group.Links)
                    link.Validate();
                break;

            case FooterTextBlock text:
                if (string.IsNullOrWhiteSpace(text.Text))
                    throw new InvalidOperationException("Footer text block requires text.");
                break;

            case FooterSocialLinks social:
                foreach (var link in social.Links)
                    link.Validate();
                break;

            case FooterNewsletterSignup newsletter:
                if (string.IsNullOrWhiteSpace(newsletter.EndpointKey))
                    throw new InvalidOperationException("Footer newsletter signup requires an endpoint key.");
                break;

            case FooterSearch search:
                if (string.IsNullOrWhiteSpace(search.SearchAction))
                    throw new InvalidOperationException("Footer search requires an action route.");
                break;
        }
    }
}

public sealed record FooterCanvasRow
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public string? Label { get; init; }
    public string DesktopDisplay { get; init; } = "Grid";
    public string TabletDisplay { get; init; } = "Grid";
    public string MobileDisplay { get; init; } = "Stack";
    public List<FooterCanvasColumn> Columns { get; init; } = [];
}

public sealed record FooterCanvasColumn
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public int DesktopSpan { get; init; } = 4;
    public int TabletSpan { get; init; } = 6;
    public int MobileSpan { get; init; } = 12;
    public List<FooterCanvasBlock> Blocks { get; init; } = [];
}

public sealed record FooterCanvasBlock
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public IFooterComponent Component { get; init; } = new FooterTextBlock { Text = "Footer text" };
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FooterLinkGroup), "linkGroup")]
[JsonDerivedType(typeof(FooterTextBlock), "text")]
[JsonDerivedType(typeof(FooterSocialLinks), "social")]
[JsonDerivedType(typeof(FooterNewsletterSignup), "newsletter")]
[JsonDerivedType(typeof(FooterSearch), "search")]
[JsonDerivedType(typeof(FooterSpacer), "spacer")]
public interface IFooterComponent
{
    string Key { get; }
    int Order { get; }
    FooterSectionPlacement Placement { get; }
}

public enum FooterSectionPlacement
{
    Brand,
    Main,
    Utility,
    Bottom
}

public sealed record FooterLinkGroup : IFooterComponent
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Main;
    public string Title { get; init; } = string.Empty;
    public List<FooterLink> Links { get; init; } = [];
}

public sealed record FooterTextBlock : IFooterComponent
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Brand;
    public string Text { get; init; } = string.Empty;
}

public sealed record FooterSocialLinks : IFooterComponent
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;
    public List<FooterSocialLink> Links { get; init; } = [];
}

public sealed record FooterNewsletterSignup : IFooterComponent
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;
    public string EndpointKey { get; init; } = string.Empty;
    public string Placeholder { get; init; } = "Email address";
    public string ButtonLabel { get; init; } = "Subscribe";
}

public sealed record FooterSearch : IFooterComponent
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;
    public string Placeholder { get; init; } = "Search...";
    public string SearchAction { get; init; } = "/search";
}

public sealed record FooterSpacer : IFooterComponent
{
    public string Key { get; init; } = string.Empty;
    public int Order { get; init; }
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Main;
    public string SizeToken { get; init; } = "md";
}

public sealed record FooterBrandSettings
{
    public string? LogoUrl { get; init; }
    public string? LogoAltText { get; init; }
    public string CompanyName { get; init; } = "Aero CMS";
    public string? Tagline { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            throw new InvalidOperationException("Footer company name is required.");
        }

        FooterUrlValidation.Validate(LogoUrl, "Footer logo URL");
    }
}

public sealed record FooterStyleSettings
{
    public string? BackgroundColorToken { get; init; } = "slate-950";
    public string? TextColorToken { get; init; } = "slate-100";
    public string? AccentColorToken { get; init; } = "indigo-300";
    public string? BackgroundImageUrl { get; init; }
    public string BackgroundImageMode { get; init; } = "cover";
    public string? OverlayColorToken { get; init; } = "slate-950";
    public decimal OverlayOpacity { get; init; } = 0.35m;
    public string PaddingToken { get; init; } = "footer";

    public static FooterStyleSettings Default { get; } = new();

    public void Validate()
    {
        FooterUrlValidation.Validate(BackgroundImageUrl, "Footer background image URL");

        if (OverlayOpacity is < 0 or > 1)
        {
            throw new InvalidOperationException("Footer overlay opacity must be between 0 and 1.");
        }
    }
}

public sealed record FooterResponsiveSettings(string MobileBreakpoint)
{
    public static FooterResponsiveSettings Default { get; } = new("md");
}

public sealed record FooterLegalSettings
{
    public string? CopyrightText { get; init; }
    public bool AutoAppendCurrentYear { get; init; } = true;
    public List<FooterLink> LegalLinks { get; init; } = [];

    public static FooterLegalSettings Default { get; } = new()
    {
        CopyrightText = "Aero CMS. All rights reserved."
    };
}

public sealed record FooterLink(string Label, string Href, bool OpenInNewTab = false, long Id = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException("Footer link label is required.");

        if (string.IsNullOrWhiteSpace(Href))
            throw new InvalidOperationException("Footer link href is required.");

        FooterUrlValidation.Validate(Href, "Footer link URL");
    }
}

public sealed record FooterSocialLink(string Platform, string Href)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Platform))
            throw new InvalidOperationException("Footer social platform is required.");

        FooterUrlValidation.Validate(Href, "Footer social URL");
    }
}

internal static class FooterUrlValidation
{
    public static void Validate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return;
        }

        throw new InvalidOperationException($"{fieldName} must be a relative URL or absolute HTTP/HTTPS URL.");
    }
}
