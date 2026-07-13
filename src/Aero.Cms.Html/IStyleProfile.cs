namespace Aero.Cms.Html;

/// <summary>
/// Supplies site-level values used while compiling framework-neutral style intent.
/// </summary>
public interface IStyleProfile
{
    string ProfileId { get; }
    string ProfileVersion { get; }
    decimal SmallScreenBreakpointRem { get; }
    IReadOnlyDictionary<string, string> ColorTokens { get; }
}

/// <summary>
/// Default native-CSS profile for sites without a framework adapter.
/// </summary>
public sealed record NativeStyleProfile : IStyleProfile
{
    public string ProfileId { get; init; } = "aero-native";
    public string ProfileVersion { get; init; } = "1";
    public decimal SmallScreenBreakpointRem { get; init; } = 48;
    public IReadOnlyDictionary<string, string> ColorTokens { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
