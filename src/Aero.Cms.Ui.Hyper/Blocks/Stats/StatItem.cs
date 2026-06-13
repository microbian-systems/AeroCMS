namespace Aero.Cms.Ui.Hyper.Blocks.Stats;

/// <summary>
/// Represents a single stat item (label + value) used across Stats blocks.
/// </summary>
public sealed class StatItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
