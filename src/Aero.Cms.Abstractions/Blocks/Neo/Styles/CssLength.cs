using System.Globalization;
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A validated CSS length supported by the visual editor.
/// </summary>
public readonly record struct CssLength(decimal? Value, CssLengthUnit Unit)
{
        /// <summary>
    /// Gets or sets the Auto.
    /// </summary>
public static CssLength Auto => new(null, CssLengthUnit.Auto);

        /// <summary>
    /// ToString method.
    /// </summary>
public override string ToString()
    {
        if (Unit == CssLengthUnit.Auto)
        {
            return "auto";
        }

        return string.Concat(
            Value?.ToString(CultureInfo.InvariantCulture),
            Unit.ToCssSuffix());
    }
}

/// <summary>
/// CSS length units supported by the visual editor.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CssLengthUnit
{
    Pixels,
    Percent,
    Rem,
    Em,
    ViewportWidth,
    ViewportHeight,
    Auto
}

/// <summary>
/// Represents a class for CssLengthUnitExtensions.
/// </summary>
public static class CssLengthUnitExtensions
{
        /// <summary>
    /// ToCssSuffix method.
    /// </summary>
public static string ToCssSuffix(this CssLengthUnit unit) =>
        unit switch
        {
            CssLengthUnit.Pixels => "px",
            CssLengthUnit.Percent => "%",
            CssLengthUnit.Rem => "rem",
            CssLengthUnit.Em => "em",
            CssLengthUnit.ViewportWidth => "vw",
            CssLengthUnit.ViewportHeight => "vh",
            CssLengthUnit.Auto => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
        };
}
