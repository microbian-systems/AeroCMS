using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Footer.Domain;

/// <summary>
/// Captures the complete editable or published footer composition.
/// </summary>
/// <remarks>
/// A snapshot can use either the row/column canvas model or the flat <see cref="Sections"/> model.
/// When any rows exist, <see cref="Components"/> and validation ignore the flat sections.
/// </remarks>
public sealed record FooterSnapshot
{
    /// <summary>Gets the logo, company name, and tagline content.</summary>
    public FooterBrandSettings Brand { get; init; } = new();

    /// <summary>Gets the visual settings supplied to the renderer.</summary>
    public FooterStyleSettings Style { get; init; } = FooterStyleSettings.Default;

    /// <summary>Gets the responsive metadata stored with the snapshot.</summary>
    public FooterResponsiveSettings Responsive { get; init; } = FooterResponsiveSettings.Default;

    /// <summary>Gets the copyright and legal-link content.</summary>
    public FooterLegalSettings Legal { get; init; } = FooterLegalSettings.Default;

    /// <summary>Gets the ordered canvas rows used by the structured layout model.</summary>
    public List<FooterCanvasRow> Rows { get; init; } = [];

    /// <summary>Gets the flat component list used when <see cref="Rows"/> is empty.</summary>
    public List<IFooterComponent> Sections { get; init; } = [];

    /// <summary>
    /// Enumerates the active components in render order.
    /// </summary>
    /// <remarks>
    /// Rows, columns, and blocks are independently ordered by their <c>Order</c> values. When rows
    /// exist, flat sections are not returned. The property is excluded from JSON serialization.
    /// </remarks>
    [JsonIgnore]
    public IEnumerable<IFooterComponent> Components => Rows.Count > 0
        ? Rows.OrderBy(row => row.Order)
            .SelectMany(row => row.Columns.OrderBy(column => column.Order))
            .SelectMany(column => column.Blocks.OrderBy(block => block.Order).Select(block => block.Component))
        : Sections.OrderBy(x => x.Order);

    /// <summary>
    /// Gets a shared snapshot initialized with the model defaults and no components.
    /// </summary>
    /// <remarks>The returned record contains mutable lists; callers should not mutate shared state.</remarks>
    public static FooterSnapshot Empty { get; } = new();

    /// <summary>
    /// Validates the brand, style, active component keys, and component-specific required content.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required content is missing, component keys are duplicated case-insensitively,
    /// opacity is outside zero through one, or a validated URL is not app-relative HTTP/HTTPS.
    /// </exception>
    /// <remarks>
    /// This validation does not sanitize content and does not validate row, column, or block keys,
    /// ordering values, responsive tokens, or every style token.
    /// </remarks>
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
/// Defines one ordered row in the structured footer canvas.
/// </summary>
public sealed record FooterCanvasRow
{
    /// <summary>Gets the authoring key for the row.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the row's relative sort position.</summary>
    public int Order { get; init; }

    /// <summary>Gets the optional author-facing label.</summary>
    public string? Label { get; init; }

    /// <summary>Gets the stored desktop display-mode token.</summary>
    public string DesktopDisplay { get; init; } = "Grid";

    /// <summary>Gets the stored tablet display-mode token.</summary>
    public string TabletDisplay { get; init; } = "Grid";

    /// <summary>Gets the stored mobile display-mode token.</summary>
    public string MobileDisplay { get; init; } = "Stack";

    /// <summary>Gets the columns contained by the row.</summary>
    public List<FooterCanvasColumn> Columns { get; init; } = [];
}

/// <summary>
/// Defines one ordered column and its responsive spans in a canvas row.
/// </summary>
public sealed record FooterCanvasColumn
{
    /// <summary>Gets the authoring key for the column.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the column's relative sort position.</summary>
    public int Order { get; init; }

    /// <summary>Gets the requested desktop width on a twelve-column grid.</summary>
    public int DesktopSpan { get; init; } = 4;

    /// <summary>Gets the requested tablet width on a twelve-column grid.</summary>
    public int TabletSpan { get; init; } = 6;

    /// <summary>Gets the requested mobile width on a twelve-column grid.</summary>
    public int MobileSpan { get; init; } = 12;

    /// <summary>Gets the component blocks contained by the column.</summary>
    public List<FooterCanvasBlock> Blocks { get; init; } = [];
}

/// <summary>
/// Places one ordered footer component in a canvas column.
/// </summary>
public sealed record FooterCanvasBlock
{
    /// <summary>Gets the authoring key for the block.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the block's relative sort position.</summary>
    public int Order { get; init; }

    /// <summary>Gets the component hosted by the block.</summary>
    public IFooterComponent Component { get; init; } = new FooterTextBlock { Text = "Footer text" };
}

/// <summary>
/// Defines the identity, order, and intended placement shared by footer components.
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
    /// <summary>Gets the key that must be unique across active components.</summary>
    string Key { get; }

    /// <summary>Gets the component's relative sort position.</summary>
    int Order { get; }

    /// <summary>Gets the component's intended semantic footer region.</summary>
    FooterSectionPlacement Placement { get; }
}

/// <summary>
/// Identifies the intended semantic region for a flat footer component.
/// </summary>
public enum FooterSectionPlacement
{
    /// <summary>The brand and descriptive-content region.</summary>
    Brand,

    /// <summary>The primary navigation or content region.</summary>
    Main,

    /// <summary>The supporting utility region.</summary>
    Utility,

    /// <summary>The bottom legal and social region.</summary>
    Bottom
}

/// <summary>
/// Defines a titled collection of navigation links.
/// </summary>
public sealed record FooterLinkGroup : IFooterComponent
{
    /// <inheritdoc />
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Main;

    /// <summary>Gets the heading displayed above the links.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the links in their stored order.</summary>
    public List<FooterLink> Links { get; init; } = [];
}

/// <summary>
/// Defines a plain-text footer component.
/// </summary>
public sealed record FooterTextBlock : IFooterComponent
{
    /// <inheritdoc />
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Brand;

    /// <summary>Gets the text content. The HTML renderer encodes this value.</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// Defines a collection of links to social platforms.
/// </summary>
public sealed record FooterSocialLinks : IFooterComponent
{
    /// <inheritdoc />
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;

    /// <summary>Gets the social links in their stored order.</summary>
    public List<FooterSocialLink> Links { get; init; } = [];
}

/// <summary>
/// Defines a newsletter-subscription form rendered into the footer.
/// </summary>
public sealed record FooterNewsletterSignup : IFooterComponent
{
    /// <inheritdoc />
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;

    /// <summary>Gets the form action written by the renderer.</summary>
    /// <remarks>This module renders the form but does not implement the receiving endpoint.</remarks>
    public string EndpointKey { get; init; } = string.Empty;

    /// <summary>Gets the email-input placeholder.</summary>
    public string Placeholder { get; init; } = "Email address";

    /// <summary>Gets the submit-button label.</summary>
    public string ButtonLabel { get; init; } = "Subscribe";
}

/// <summary>
/// Defines a search form rendered into the footer.
/// </summary>
public sealed record FooterSearch : IFooterComponent
{
    /// <inheritdoc />
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Utility;

    /// <summary>Gets the search-input placeholder.</summary>
    public string Placeholder { get; init; } = "Search...";

    /// <summary>Gets the action written on the rendered GET form.</summary>
    /// <remarks>This module renders the form but does not implement the search endpoint.</remarks>
    public string SearchAction { get; init; } = "/search";
}

/// <summary>
/// Defines a visual spacing component.
/// </summary>
public sealed record FooterSpacer : IFooterComponent
{
    /// <inheritdoc />
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public FooterSectionPlacement Placement { get; init; } = FooterSectionPlacement.Main;

    /// <summary>Gets the size token written as a CSS class suffix by the renderer.</summary>
    public string SizeToken { get; init; } = "md";
}

/// <summary>
/// Defines the brand identity displayed by the footer renderer.
/// </summary>
public sealed record FooterBrandSettings
{
    /// <summary>Gets the optional app-relative or absolute HTTP/HTTPS logo URL.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Gets the alternative text used when a logo image is rendered.</summary>
    public string? LogoAltText { get; init; }

    /// <summary>Gets the required company name.</summary>
    public string CompanyName { get; init; } = "Aero CMS";

    /// <summary>Gets the optional tagline displayed beneath the company name.</summary>
    public string? Tagline { get; init; }

    /// <summary>
    /// Validates the required company name and logo URL shape.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the company name is blank or the nonblank logo URL is not app-relative HTTP/HTTPS.
    /// </exception>
    /// <remarks>This method validates URL shape; it does not fetch, authorize, or sanitize the resource.</remarks>
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
/// Defines visual tokens and optional background-image settings for a footer.
/// </summary>
public sealed record FooterStyleSettings
{
    /// <summary>Gets the stored background-color token.</summary>
    public string? BackgroundColorToken { get; init; } = "slate-950";

    /// <summary>Gets the stored text-color token.</summary>
    public string? TextColorToken { get; init; } = "slate-100";

    /// <summary>Gets the stored accent-color token.</summary>
    public string? AccentColorToken { get; init; } = "indigo-300";

    /// <summary>Gets the optional app-relative or absolute HTTP/HTTPS background-image URL.</summary>
    public string? BackgroundImageUrl { get; init; }

    /// <summary>Gets the requested background-image mode.</summary>
    /// <remarks>The current renderer recognizes cover and contain; other values render a repeating image.</remarks>
    public string BackgroundImageMode { get; init; } = "cover";

    /// <summary>Gets the stored overlay-color token.</summary>
    public string? OverlayColorToken { get; init; } = "slate-950";

    /// <summary>Gets the requested overlay opacity in the inclusive range zero through one.</summary>
    public decimal OverlayOpacity { get; init; } = 0.35m;

    /// <summary>Gets the stored padding token.</summary>
    public string PaddingToken { get; init; } = "footer";

    /// <summary>Gets a shared instance initialized with the style defaults.</summary>
    public static FooterStyleSettings Default { get; } = new();

    /// <summary>
    /// Validates the background-image URL shape and overlay opacity.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the nonblank image URL is not app-relative HTTP/HTTPS or opacity is outside zero through one.
    /// </exception>
    /// <remarks>This method does not validate the color, mode, or padding tokens.</remarks>
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
/// Stores the breakpoint token associated with a footer snapshot.
/// </summary>
/// <param name="MobileBreakpoint">The mobile breakpoint token. The model does not validate this value.</param>
public sealed record FooterResponsiveSettings(string MobileBreakpoint)
{
    /// <summary>Gets a shared settings instance using the <c>md</c> breakpoint.</summary>
    public static FooterResponsiveSettings Default { get; } = new("md");
}

/// <summary>
/// Defines copyright text and legal links displayed in the footer's bottom region.
/// </summary>
public sealed record FooterLegalSettings
{
    /// <summary>Gets the optional copyright text.</summary>
    public string? CopyrightText { get; init; }

    /// <summary>Gets whether the UTC calendar year is appended during rendering.</summary>
    public bool AutoAppendCurrentYear { get; init; } = true;

    /// <summary>Gets the legal links in their stored order.</summary>
    public List<FooterLink> LegalLinks { get; init; } = [];

    /// <summary>Gets a shared instance initialized with the default Aero CMS copyright text.</summary>
    /// <remarks>The returned record contains a mutable link list; callers should not mutate shared state.</remarks>
    public static FooterLegalSettings Default { get; } = new()
    {
        CopyrightText = "Aero CMS. All rights reserved."
    };
}

/// <summary>
/// Defines a labeled footer hyperlink.
/// </summary>
/// <param name="Label">The required display label.</param>
/// <param name="Href">The required app-relative or absolute HTTP/HTTPS destination.</param>
/// <param name="OpenInNewTab">Whether the renderer requests a new browsing context.</param>
/// <param name="Id">The optional persisted link identifier.</param>
public sealed record FooterLink(string Label, string Href, bool OpenInNewTab = false, long Id = 0)
{
    /// <summary>
    /// Validates the required label and destination URL shape.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the label or destination is blank, or the destination is not app-relative HTTP/HTTPS.
    /// </exception>
    /// <remarks>This method validates URL shape; it does not sanitize or verify the destination.</remarks>
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
/// Defines a link to a named social platform.
/// </summary>
/// <param name="Platform">The required platform label.</param>
/// <param name="Href">The optional app-relative or absolute HTTP/HTTPS destination.</param>
public sealed record FooterSocialLink(string Platform, string Href)
{
    /// <summary>
    /// Validates the platform label and, when nonblank, the destination URL shape.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the platform is blank or a nonblank destination is not app-relative HTTP/HTTPS.
    /// </exception>
    /// <remarks>
    /// Unlike <see cref="FooterLink.Validate"/>, this method currently permits a blank destination.
    /// It validates URL shape but does not sanitize or verify the destination.
    /// </remarks>
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
    /// Accepts blank values, single-slash app-relative URLs, and absolute HTTP or HTTPS URLs.
    /// </summary>
    /// <param name="value">The URL value to validate.</param>
    /// <param name="fieldName">The field label included in an error message.</param>
    /// <exception cref="InvalidOperationException">Thrown when a nonblank value has an unsupported URL shape or scheme.</exception>
    /// <remarks>This check is validation, not content sanitization or destination authorization.</remarks>
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
