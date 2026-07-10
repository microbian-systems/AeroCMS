namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A constrained two-stop gradient rendered by the visual editor.
/// </summary>
public sealed record LinearGradient
{
        /// <summary>
    /// Gets or sets the Enabled.
    /// </summary>
public bool Enabled { get; init; } = true;

        /// <summary>
    /// Gets or sets the Type.
    /// </summary>
public GradientType Type { get; init; } = GradientType.Linear;

        /// <summary>
    /// Gets or sets the Angle.
    /// </summary>
public decimal Angle { get; init; } = 180m;

        /// <summary>
    /// Gets or sets the Start Color.
    /// </summary>
public CssColor StartColor { get; init; } = new("#ffffff");

        /// <summary>
    /// Gets or sets the End Color.
    /// </summary>
public CssColor EndColor { get; init; } = new("#000000");

        /// <summary>
    /// Gets or sets the Start Position.
    /// </summary>
public decimal StartPosition { get; init; }

        /// <summary>
    /// Gets or sets the End Position.
    /// </summary>
public decimal EndPosition { get; init; } = 100m;

        /// <summary>
    /// Gets or sets the Radial Shape.
    /// </summary>
public RadialGradientShape RadialShape { get; init; } = RadialGradientShape.Ellipse;

        /// <summary>
    /// Gets or sets the Radial Position.
    /// </summary>
public RadialGradientPosition RadialPosition { get; init; } = RadialGradientPosition.Center;
}

/// <summary>
/// Defines an enumeration for GradientType.
/// </summary>
public enum GradientType
{
    Linear,
    Radial
}

/// <summary>
/// Defines an enumeration for RadialGradientShape.
/// </summary>
public enum RadialGradientShape
{
    Circle,
    Ellipse
}

/// <summary>
/// Defines an enumeration for RadialGradientPosition.
/// </summary>
public enum RadialGradientPosition
{
    Center,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    TopLeft
}
