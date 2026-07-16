namespace Aero.Cms.Html;

/// <summary>
/// Persisted, framework-neutral site style-profile settings.
/// </summary>
public sealed class StyleProfileSettings
{
    public long Revision { get; set; } = 1;

    public decimal SmallScreenBreakpointRem { get; set; } = 48;

    public List<StyleColorToken> ColorTokens { get; set; } = [];
}

/// <summary>
/// A named site color stored as a canonical CSS hexadecimal value.
/// </summary>
public sealed class StyleColorToken
{
    public string Name { get; set; } = string.Empty;

    public string HexValue { get; set; } = string.Empty;
}
