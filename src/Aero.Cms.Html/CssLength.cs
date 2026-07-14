namespace Aero.Cms.Html;

/// <summary>
/// A constrained CSS length used by semantic editor controls.
/// </summary>
public sealed class CssLength
{
    public decimal Value { get; set; }
    public CssLengthUnit Unit { get; set; }

    public static CssLength Pixels(decimal value) => new() { Value = value, Unit = CssLengthUnit.Pixel };
    public static CssLength Rem(decimal value) => new() { Value = value, Unit = CssLengthUnit.Rem };
    public static CssLength Em(decimal value) => new() { Value = value, Unit = CssLengthUnit.Em };
    public static CssLength Percent(decimal value) => new() { Value = value, Unit = CssLengthUnit.Percent };
    public static CssLength ViewportHeight(decimal value) => new() { Value = value, Unit = CssLengthUnit.ViewportHeight };
    public static CssLength ViewportWidth(decimal value) => new() { Value = value, Unit = CssLengthUnit.ViewportWidth };
}

/// <summary>
/// Supported units for constrained CSS lengths.
/// </summary>
public enum CssLengthUnit
{
    Pixel,
    Rem,
    Em,
    Percent,
    ViewportHeight,
    ViewportWidth
}
