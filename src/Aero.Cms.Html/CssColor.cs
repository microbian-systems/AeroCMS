namespace Aero.Cms.Html;

/// <summary>
/// A safe color reference expressed as a literal hex value or a site design token.
/// </summary>
public sealed class CssColor
{
    /// <summary>Gets or sets whether <see cref="Value"/> is a hexadecimal literal or a profile token.</summary>
    public CssColorKind Kind { get; set; }

    /// <summary>Gets or sets the hexadecimal color or case-sensitive profile token name.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Creates a literal hexadecimal color reference.</summary>
    /// <param name="value">The hexadecimal value. Validation is deferred to style-profile compilation.</param>
    /// <returns>A color reference containing the supplied value.</returns>
    public static CssColor Hex(string value) => new() { Kind = CssColorKind.Hex, Value = value };

    /// <summary>Creates a reference to a color defined by the active style profile.</summary>
    /// <param name="token">The case-sensitive token name. Resolution is deferred to style compilation.</param>
    /// <returns>A color reference containing the supplied token.</returns>
    public static CssColor Token(string token) => new() { Kind = CssColorKind.ThemeToken, Value = token };
}

/// <summary>Identifies how a semantic color value must be resolved.</summary>
public enum CssColorKind
{
    /// <summary>The value is a hexadecimal CSS color.</summary>
    Hex,

    /// <summary>The value names a color in the active style profile.</summary>
    ThemeToken
}
