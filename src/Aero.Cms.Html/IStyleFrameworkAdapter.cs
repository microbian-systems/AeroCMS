namespace Aero.Cms.Html;

/// <summary>
/// Maps only exact framework-neutral style intents to framework classes.
/// Unmapped intent is returned for deterministic native-CSS fallback.
/// </summary>
public interface IStyleFrameworkAdapter
{
    /// <summary>Gets the stable framework identifier included in compilation metadata.</summary>
    string AdapterId { get; }
    /// <summary>Gets the mapping-contract version included in compilation metadata.</summary>
    string AdapterVersion { get; }

    /// <summary>
    /// Maps only style intent that the framework can represent exactly.
    /// </summary>
    /// <param name="style">The source style. Implementations must not mutate this instance.</param>
    /// <param name="profile">The active style profile and responsive breakpoint.</param>
    /// <returns>Framework classes and an independent residual style for native-CSS fallback.</returns>
    FrameworkStyleMapping Map(HtmlStyle style, IStyleProfile profile);
}

/// <summary>
/// Result of one framework mapping pass. Adapters must not mutate the source style.
/// </summary>
/// <param name="Classes">The exact framework classes emitted for the source intent.</param>
/// <param name="ResidualStyle">Style intent that still requires native CSS, or <see langword="null"/> when fully mapped.</param>
public sealed record FrameworkStyleMapping(
    IReadOnlyList<string> Classes,
    HtmlStyle? ResidualStyle);
