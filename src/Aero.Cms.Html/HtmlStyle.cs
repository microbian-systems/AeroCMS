namespace Aero.Cms.Html;

/// <summary>
/// Small, framework-neutral layout intent configured by ordinary editor controls.
/// It deliberately does not model arbitrary CSS declarations or pseudo-states.
/// </summary>
public sealed class HtmlStyle
{
    public CssDisplay? Display { get; set; }
    public CssFlexDirection? FlexDirection { get; set; }
    public int? GridColumns { get; set; }
    public bool StackOnSmallScreens { get; set; }
    public CssLength? Gap { get; set; }
    public CssAlignment? AlignItems { get; set; }
    public CssJustification? JustifyContent { get; set; }
    public CssLogicalSpacing? Padding { get; set; }
    public CssLogicalSpacing? Margin { get; set; }
    public CssLength? MinimumHeight { get; set; }
    public CssSurfaceStyle? Surface { get; set; }
    public CssTypographyStyle? Typography { get; set; }
}
