namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A constrained two-stop gradient rendered by the visual editor.
/// </summary>
public sealed record LinearGradient
{
    public bool Enabled { get; init; } = true;

    public GradientType Type { get; init; } = GradientType.Linear;

    public decimal Angle { get; init; } = 180m;

    public CssColor StartColor { get; init; } = new("#ffffff");

    public CssColor EndColor { get; init; } = new("#000000");

    public decimal StartPosition { get; init; }

    public decimal EndPosition { get; init; } = 100m;

    public RadialGradientShape RadialShape { get; init; } = RadialGradientShape.Ellipse;

    public RadialGradientPosition RadialPosition { get; init; } = RadialGradientPosition.Center;
}

public enum GradientType
{
    Linear,
    Radial
}

public enum RadialGradientShape
{
    Circle,
    Ellipse
}

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
