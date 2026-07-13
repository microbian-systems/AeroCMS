namespace Aero.Cms.Html;

/// <summary>
/// Framework-neutral typography intent exposed by the page editor's text controls.
/// </summary>
public sealed class CssTypographyStyle
{
    public CssColor? Color { get; set; }
    public CssLength? FontSize { get; set; }
    public int? FontWeight { get; set; }
    public decimal? LineHeight { get; set; }
    public CssLength? LetterSpacing { get; set; }
    public CssTextAlignment? Alignment { get; set; }
    public CssTextGradient? Gradient { get; set; }
}

public sealed class CssTextGradient
{
    public CssColor StartColor { get; set; } = CssColor.Hex("#000000");
    public CssColor EndColor { get; set; } = CssColor.Hex("#ffffff");
    public decimal AngleDegrees { get; set; } = 90;
}

public enum CssTextAlignment
{
    Start,
    Center,
    End,
    Justify
}
