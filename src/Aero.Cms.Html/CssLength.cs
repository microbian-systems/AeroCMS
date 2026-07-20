namespace Aero.Cms.Html;

/// <summary>
/// A constrained CSS length used by semantic editor controls.
/// </summary>
public sealed class CssLength
{
    /// <summary>Gets or sets the numeric component of the length.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the allowlisted CSS unit applied to <see cref="Value"/>.</summary>
    public CssLengthUnit Unit { get; set; }

    /// <summary>Creates an absolute pixel length.</summary>
    /// <param name="value">The numeric pixel value.</param>
    /// <returns>A pixel length.</returns>
    public static CssLength Pixels(decimal value) => new() { Value = value, Unit = CssLengthUnit.Pixel };

    /// <summary>Creates a length relative to the root element's font size.</summary>
    /// <param name="value">The numeric root-em value.</param>
    /// <returns>A root-em length.</returns>
    public static CssLength Rem(decimal value) => new() { Value = value, Unit = CssLengthUnit.Rem };

    /// <summary>Creates a length relative to the current element's font size.</summary>
    /// <param name="value">The numeric em value.</param>
    /// <returns>An em length.</returns>
    public static CssLength Em(decimal value) => new() { Value = value, Unit = CssLengthUnit.Em };

    /// <summary>Creates a percentage length.</summary>
    /// <param name="value">The numeric percentage without a percent suffix.</param>
    /// <returns>A percentage length.</returns>
    public static CssLength Percent(decimal value) => new() { Value = value, Unit = CssLengthUnit.Percent };

    /// <summary>Creates a length relative to viewport height.</summary>
    /// <param name="value">The numeric viewport-height value.</param>
    /// <returns>A viewport-height length.</returns>
    public static CssLength ViewportHeight(decimal value) => new() { Value = value, Unit = CssLengthUnit.ViewportHeight };

    /// <summary>Creates a length relative to viewport width.</summary>
    /// <param name="value">The numeric viewport-width value.</param>
    /// <returns>A viewport-width length.</returns>
    public static CssLength ViewportWidth(decimal value) => new() { Value = value, Unit = CssLengthUnit.ViewportWidth };
}

/// <summary>
/// Supported units for constrained CSS lengths.
/// </summary>
public enum CssLengthUnit
{
    /// <summary>CSS pixels.</summary>
    Pixel,
    /// <summary>Root-element font-size units.</summary>
    Rem,
    /// <summary>Current-element font-size units.</summary>
    Em,
    /// <summary>Percentage units.</summary>
    Percent,
    /// <summary>Viewport-height units.</summary>
    ViewportHeight,
    /// <summary>Viewport-width units.</summary>
    ViewportWidth
}
