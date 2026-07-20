namespace Aero.Cms.Html;

/// <summary>
/// Small, framework-neutral layout intent configured by ordinary editor controls.
/// It deliberately does not model arbitrary CSS declarations or pseudo-states.
/// </summary>
public sealed class HtmlStyle
{
    /// <summary>Gets or sets the element's layout display mode.</summary>
    public CssDisplay? Display { get; set; }
    /// <summary>Gets or sets the main-axis direction when the element is a flex container.</summary>
    public CssFlexDirection? FlexDirection { get; set; }
    /// <summary>Gets or sets the number of equal grid columns.</summary>
    public int? GridColumns { get; set; }
    /// <summary>Gets or sets whether a multi-column layout becomes a single column below the profile breakpoint.</summary>
    public bool StackOnSmallScreens { get; set; }
    /// <summary>Gets or sets spacing between flex or grid children.</summary>
    public CssLength? Gap { get; set; }
    /// <summary>Gets or sets child alignment on the cross axis.</summary>
    public CssAlignment? AlignItems { get; set; }
    /// <summary>Gets or sets child distribution on the main axis.</summary>
    public CssJustification? JustifyContent { get; set; }
    /// <summary>Gets or sets direction-neutral inner spacing.</summary>
    public CssLogicalSpacing? Padding { get; set; }
    /// <summary>Gets or sets direction-neutral outer spacing.</summary>
    public CssLogicalSpacing? Margin { get; set; }
    /// <summary>Gets or sets the minimum block size of the element.</summary>
    public CssLength? MinimumHeight { get; set; }
    /// <summary>Gets or sets background and corner styling.</summary>
    public CssSurfaceStyle? Surface { get; set; }
    /// <summary>Gets or sets typography styling.</summary>
    public CssTypographyStyle? Typography { get; set; }
}
