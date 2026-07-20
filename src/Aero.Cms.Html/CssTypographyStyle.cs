namespace Aero.Cms.Html;

/// <summary>
/// Framework-neutral typography intent exposed by the page editor's text controls.
/// </summary>
public sealed class CssTypographyStyle
{
    /// <summary>Gets or sets the foreground text color.</summary>
    public CssColor? Color { get; set; }
    /// <summary>Gets or sets the font size.</summary>
    public CssLength? FontSize { get; set; }
    /// <summary>Gets or sets the numeric CSS font weight.</summary>
    public int? FontWeight { get; set; }
    /// <summary>Gets or sets the unitless line-height multiplier.</summary>
    public decimal? LineHeight { get; set; }
    /// <summary>Gets or sets additional spacing between glyphs.</summary>
    public CssLength? LetterSpacing { get; set; }
    /// <summary>Gets or sets logical text alignment.</summary>
    public CssTextAlignment? Alignment { get; set; }
    /// <summary>Gets or sets an optional text gradient that supersedes the solid foreground color.</summary>
    public CssTextGradient? Gradient { get; set; }
}

/// <summary>Describes a two-stop linear gradient clipped to text glyphs.</summary>
public sealed class CssTextGradient
{
    /// <summary>Gets or sets the gradient's starting color.</summary>
    public CssColor StartColor { get; set; } = CssColor.Hex("#000000");
    /// <summary>Gets or sets the gradient's ending color.</summary>
    public CssColor EndColor { get; set; } = CssColor.Hex("#ffffff");
    /// <summary>Gets or sets the CSS linear-gradient angle in degrees.</summary>
    public decimal AngleDegrees { get; set; } = 90;
}

/// <summary>Defines logical text alignment independent of writing direction.</summary>
public enum CssTextAlignment
{
    /// <summary>Aligns text at the inline start.</summary>
    Start,
    /// <summary>Centers text.</summary>
    Center,
    /// <summary>Aligns text at the inline end.</summary>
    End,
    /// <summary>Adjusts spacing so line edges align on both sides.</summary>
    Justify
}
