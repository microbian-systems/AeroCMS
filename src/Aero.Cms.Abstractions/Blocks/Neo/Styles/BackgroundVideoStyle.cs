namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Describes a decorative video layer rendered behind a node's content.
/// </summary>
public sealed record BackgroundVideoStyle
{
    public bool Enabled { get; init; } = true;

    public long MediaId { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? PosterUrl { get; init; }

    public bool Autoplay { get; init; } = true;

    public bool Muted { get; init; } = true;

    public bool Loop { get; init; } = true;

    public bool PlaysInline { get; init; } = true;
}
