namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Describes a decorative video layer rendered behind a node's content.
/// </summary>
public sealed record BackgroundVideoStyle
{
        /// <summary>
    /// Gets or sets the Enabled.
    /// </summary>
public bool Enabled { get; init; } = true;

        /// <summary>
    /// Gets or sets the Media Id.
    /// </summary>
public long MediaId { get; init; }

        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; init; } = string.Empty;

        /// <summary>
    /// Gets or sets the Poster Url.
    /// </summary>
public string? PosterUrl { get; init; }

        /// <summary>
    /// Gets or sets the Autoplay.
    /// </summary>
public bool Autoplay { get; init; } = true;

        /// <summary>
    /// Gets or sets the Muted.
    /// </summary>
public bool Muted { get; init; } = true;

        /// <summary>
    /// Gets or sets the Loop.
    /// </summary>
public bool Loop { get; init; } = true;

        /// <summary>
    /// Gets or sets the Plays Inline.
    /// </summary>
public bool PlaysInline { get; init; } = true;
}
