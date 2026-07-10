namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A constrained box shadow expressed in pixels.
/// </summary>
public sealed record BoxShadow
{
        /// <summary>
    /// Gets or sets the Enabled.
    /// </summary>
public bool Enabled { get; init; } = true;

        /// <summary>
    /// Gets or sets the Offset X.
    /// </summary>
public decimal OffsetX { get; init; }

        /// <summary>
    /// Gets or sets the Offset Y.
    /// </summary>
public decimal OffsetY { get; init; } = 4m;

        /// <summary>
    /// Gets or sets the Blur.
    /// </summary>
public decimal Blur { get; init; } = 12m;

        /// <summary>
    /// Gets or sets the Spread.
    /// </summary>
public decimal Spread { get; init; }

        /// <summary>
    /// Gets or sets the Color.
    /// </summary>
public CssColor Color { get; init; } = new("#00000033");
}
