using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Footer.Domain;

/// <summary>
/// Represents a record for FooterSnapshot.
/// </summary>
public sealed record FooterSnapshot
{
        /// <summary>
    /// Gets or sets the Brand.
    /// </summary>
public FooterBrandSettings Brand { get; init; } = new();
        /// <summary>
    /// Gets or sets the Style.
    /// </summary>
public FooterStyleSettings Style { get; init; } = FooterStyleSettings.Default;
        /// <summary>
    /// Gets or sets the Responsive.
    /// </summary>
public FooterResponsiveSettings Responsive { get; init; } = FooterResponsiveSettings.Default;
        /// <summary>
    /// Gets or sets the Legal.
    /// </summary>
public FooterLegalSettings Legal { get; init; } = FooterLegalSettings.Default;
        /// <summary>
    /// Gets or sets the Rows.
    /// </summary>
public List<FooterCanvasRow> Rows { get; init; } = [];
        /// <summary>
    /// Gets or sets the Sections.
    /// </summary>
public List<IFooterComponent> Sections { get; init; } = [];

        /// <summary>
    /// Gets or sets the Components.
    /// </summary>
[JsonIgnore]
    public IEnumerable<IFooterComponent> Components => Rows.Count > 0
        ? Rows.OrderBy(row => row.Order)
            .SelectMany(row => row.Columns.OrderBy(column => column.Order))
            .SelectMany(column => column.Blocks.OrderBy(block => block.Order).Select(block => block.Component))
        : Sections.OrderBy(x => x.Order);

        /// <summary>
    /// Gets or sets the Empty.
    /// </summary>
public static FooterSnapshot Empty { get; } = new();

        /// <summary>
    /// Validate method.
    /// </summary>
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

/// <summary>
/// Represents a record for FooterCanvasRow.
/// </summary>
public sealed record FooterCanvasRow
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string? Label { get; init; }
        /// <summary>
    /// Gets or sets the Desktop Display.
    /// </summary>
public string DesktopDisplay { get; init; } = "Grid";
        /// <summary>
    /// Gets or sets the Tablet Display.
    /// </summary>
public string TabletDisplay { get; init; } = "Grid";
        /// <summary>
    /// Gets or sets the Mobile Display.
    /// </summary>
public string MobileDisplay { get; init; } = "Stack";
        /// <summary>
    /// Gets or sets the Columns.
    /// </summary>
public List<FooterCanvasColumn> Columns { get; init; } = [];
}

/// <summary>
/// Represents a record for FooterCanvasColumn.
/// </summary>
public sealed record FooterCanvasColumn
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Desktop Span.
    /// </summary>
public int DesktopSpan { get; init; } = 4;
        /// <summary>
    /// Gets or sets the Tablet Span.
    /// </summary>
public int TabletSpan { get; init; } = 6;
        /// <summary>
    /// Gets or sets the Mobile Span.
    /// </summary>
public int MobileSpan { get; init; } = 12;
        /// <summary>
    /// Gets or sets the Blocks.
    /// </summary>
public List<FooterCanvasBlock> Blocks { get; init; } = [];
}

/// <summary>
/// Represents a record for FooterCanvasBlock.
/// </summary>
public sealed record FooterCanvasBlock
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Component.
    /// </summary>
public IFooterComponent Component { get; init; } = new FooterTextBlock { Text = "Footer text" };
}

/// <summary>
/// Defines an interface for IFooterComponent.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FooterLinkGroup), "linkGroup")]
[JsonDerivedType(typeof(FooterTextBlock), "text")]
[JsonDerivedType(typeof(FooterSocialLinks), "social")]
[JsonDerivedType(typeof(FooterNewsletterSignup), "newsletter")]
[JsonDerivedType(typeof(FooterSearch), "search")]
[JsonDerivedType(typeof(FooterSpacer), "spacer")]
public interface IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
string Key { get; }
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
int Order { get; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
FooterSectionPlacement Placement { get; }
}

/// <summary>
/// Defines an enumeration for FooterSectionPlacement.
/// </summary>
public enum FooterSectionPlacement
{
    Brand,
    Main,
    Utility,
    Bottom
}

/// <summary>
/// Represents a record for FooterLinkGroup.
/// </summary>
public sealed record FooterLinkGroup : IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Main;
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Links.
    /// </summary>
public List<FooterLink> Links { get; init; } = [];
}

/// <summary>
/// Represents a record for FooterTextBlock.
/// </summary>
public sealed record FooterTextBlock : IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Brand;
        /// <summary>
    /// Gets or sets the Text.
    /// </summary>
public string Text { get; init; } = string.Empty;
}

/// <summary>
/// Represents a record for FooterSocialLinks.
/// </summary>
public sealed record FooterSocialLinks : IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;
        /// <summary>
    /// Gets or sets the Links.
    /// </summary>
public List<FooterSocialLink> Links { get; init; } = [];
}

/// <summary>
/// Represents a record for FooterNewsletterSignup.
/// </summary>
public sealed record FooterNewsletterSignup : IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;
        /// <summary>
    /// Gets or sets the Endpoint Key.
    /// </summary>
public string EndpointKey { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Placeholder.
    /// </summary>
public string Placeholder { get; init; } = "Email address";
        /// <summary>
    /// Gets or sets the Button Label.
    /// </summary>
public string ButtonLabel { get; init; } = "Subscribe";
}

/// <summary>
/// Represents a record for FooterSearch.
/// </summary>
public sealed record FooterSearch : IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;
        /// <summary>
    /// Gets or sets the Placeholder.
    /// </summary>
public string Placeholder { get; init; } = "Search...";
        /// <summary>
    /// Gets or sets the Search Action.
    /// </summary>
public string SearchAction { get; init; } = "/search";
}

/// <summary>
/// Represents a record for FooterSpacer.
/// </summary>
public sealed record FooterSpacer : IFooterComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Placement.
    /// </summary>
public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Main;
        /// <summary>
    /// Gets or sets the Size Token.
    /// </summary>
public string SizeToken { get; init; } = "md";
}

/// <summary>
/// Represents a record for FooterBrandSettings.
/// </summary>
public sealed record FooterBrandSettings
{
        /// <summary>
    /// Gets or sets the Logo Url.
    /// </summary>
public string? LogoUrl { get; init; }
        /// <summary>
    /// Gets or sets the Logo Alt Text.
    /// </summary>
public string? LogoAltText { get; init; }
        /// <summary>
    /// Gets or sets the Company Name.
    /// </summary>
public string CompanyName { get; init; } = "Aero CMS";
        /// <summary>
    /// Gets or sets the Tagline.
    /// </summary>
public string? Tagline { get; init; }

        /// <summary>
    /// Validate method.
    /// </summary>
public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            throw new InvalidOperationException("Footer company name is required.");
        }

        FooterUrlValidation.Validate(LogoUrl, "Footer logo URL");
    }
}

/// <summary>
/// Represents a record for FooterStyleSettings.
/// </summary>
public sealed record FooterStyleSettings
{
        /// <summary>
    /// Gets or sets the Background Color Token.
    /// </summary>
public string? BackgroundColorToken { get; init; } = "slate-950";
        /// <summary>
    /// Gets or sets the Text Color Token.
    /// </summary>
public string? TextColorToken { get; init; } = "slate-100";
        /// <summary>
    /// Gets or sets the Accent Color Token.
    /// </summary>
public string? AccentColorToken { get; init; } = "indigo-300";
        /// <summary>
    /// Gets or sets the Background Image Url.
    /// </summary>
public string? BackgroundImageUrl { get; init; }
        /// <summary>
    /// Gets or sets the Background Image Mode.
    /// </summary>
public string BackgroundImageMode { get; init; } = "cover";
        /// <summary>
    /// Gets or sets the Overlay Color Token.
    /// </summary>
public string? OverlayColorToken { get; init; } = "slate-950";
        /// <summary>
    /// Gets or sets the Overlay Opacity.
    /// </summary>
public decimal OverlayOpacity { get; init; } = 0.35m;
        /// <summary>
    /// Gets or sets the Padding Token.
    /// </summary>
public string PaddingToken { get; init; } = "footer";

        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static FooterStyleSettings Default { get; } = new();

        /// <summary>
    /// Validate method.
    /// </summary>
public void Validate()
    {
        FooterUrlValidation.Validate(BackgroundImageUrl, "Footer background image URL");

        if (OverlayOpacity is < 0 or > 1)
        {
            throw new InvalidOperationException("Footer overlay opacity must be between 0 and 1.");
        }
    }
}

/// <summary>
/// Represents a record for FooterResponsiveSettings.
/// </summary>
public sealed record FooterResponsiveSettings(string MobileBreakpoint)
{
        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static FooterResponsiveSettings Default { get; } = new("md");
}

/// <summary>
/// Represents a record for FooterLegalSettings.
/// </summary>
public sealed record FooterLegalSettings
{
        /// <summary>
    /// Gets or sets the Copyright Text.
    /// </summary>
public string? CopyrightText { get; init; }
        /// <summary>
    /// Gets or sets the Auto Append Current Year.
    /// </summary>
public bool AutoAppendCurrentYear { get; init; } = true;
        /// <summary>
    /// Gets or sets the Legal Links.
    /// </summary>
public List<FooterLink> LegalLinks { get; init; } = [];

        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static FooterLegalSettings Default { get; } = new()
    {
        CopyrightText = "Aero CMS. All rights reserved."
    };
}

/// <summary>
/// Represents a record for FooterLink.
/// </summary>
public sealed record FooterLink(string Label, string Href, bool OpenInNewTab = false, long Id = 0)
{
        /// <summary>
    /// Validate method.
    /// </summary>
public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException("Footer link label is required.");

        if (string.IsNullOrWhiteSpace(Href))
            throw new InvalidOperationException("Footer link href is required.");

        FooterUrlValidation.Validate(Href, "Footer link URL");
    }
}

/// <summary>
/// Represents a record for FooterSocialLink.
/// </summary>
public sealed record FooterSocialLink(string Platform, string Href)
{
        /// <summary>
    /// Validate method.
    /// </summary>
public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Platform))
            throw new InvalidOperationException("Footer social platform is required.");

        FooterUrlValidation.Validate(Href, "Footer social URL");
    }
}

internal static class FooterUrlValidation
{
        /// <summary>
    /// Validate method.
    /// </summary>
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
