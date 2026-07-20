namespace Aero.Cms.Html;

/// <summary>
/// Supplies site-level values used while compiling framework-neutral style intent.
/// </summary>
public interface IStyleProfile
{
    /// <summary>Gets the stable profile identifier used in compilation metadata.</summary>
    string ProfileId { get; }
    /// <summary>Gets the profile revision used to invalidate compiled output.</summary>
    string ProfileVersion { get; }
    /// <summary>Gets the maximum width, in root-em units, for small-screen fallback rules.</summary>
    decimal SmallScreenBreakpointRem { get; }
    /// <summary>Gets canonical hexadecimal colors indexed by case-sensitive token name.</summary>
    IReadOnlyDictionary<string, string> ColorTokens { get; }
}

/// <summary>
/// Default native-CSS profile for sites without a framework adapter.
/// </summary>
public sealed record NativeStyleProfile : IStyleProfile
{
    /// <inheritdoc />
    public string ProfileId { get; init; } = "aero-native";
    /// <inheritdoc />
    public string ProfileVersion { get; init; } = "1";
    /// <inheritdoc />
    public decimal SmallScreenBreakpointRem { get; init; } = 48;
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ColorTokens { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
