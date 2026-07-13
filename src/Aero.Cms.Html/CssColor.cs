namespace Aero.Cms.Html;

/// <summary>
/// A safe color reference expressed as a literal hex value or a site design token.
/// </summary>
public sealed class CssColor
{
    public CssColorKind Kind { get; set; }
    public string Value { get; set; } = string.Empty;

    public static CssColor Hex(string value) => new() { Kind = CssColorKind.Hex, Value = value };
    public static CssColor Token(string token) => new() { Kind = CssColorKind.ThemeToken, Value = token };
}

public enum CssColorKind
{
    Hex,
    ThemeToken
}
