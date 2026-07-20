namespace Aero.Cms.Html;

/// <summary>
/// Persisted, framework-neutral site style-profile settings.
/// </summary>
public sealed class StyleProfileSettings
{
    /// <summary>Gets or sets the persisted revision used as the compiled profile version.</summary>
    public long Revision { get; set; } = 1;

    /// <summary>Gets or sets the small-screen breakpoint in root-em units.</summary>
    public decimal SmallScreenBreakpointRem { get; set; } = 48;

    /// <summary>Gets or sets the site color tokens to normalize into the style profile.</summary>
    public List<StyleColorToken> ColorTokens { get; set; } = [];
}

/// <summary>
/// A named site color stored as a canonical CSS hexadecimal value.
/// </summary>
public sealed class StyleColorToken
{
    /// <summary>Gets or sets the author-supplied token name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the author-supplied hexadecimal color.</summary>
    public string HexValue { get; set; } = string.Empty;
}
