namespace Aero.Cms.Html;

/// <summary>
/// Maps only exact framework-neutral style intents to framework classes.
/// Unmapped intent is returned for deterministic native-CSS fallback.
/// </summary>
public interface IStyleFrameworkAdapter
{
    string AdapterId { get; }
    string AdapterVersion { get; }

    FrameworkStyleMapping Map(HtmlStyle style, IStyleProfile profile);
}

/// <summary>
/// Result of one framework mapping pass. Adapters must not mutate the source style.
/// </summary>
public sealed record FrameworkStyleMapping(
    IReadOnlyList<string> Classes,
    HtmlStyle? ResidualStyle);
