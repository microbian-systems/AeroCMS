namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A constrained box shadow expressed in pixels.
/// </summary>
public sealed record BoxShadow
{
    public bool Enabled { get; init; } = true;

    public decimal OffsetX { get; init; }

    public decimal OffsetY { get; init; } = 4m;

    public decimal Blur { get; init; } = 12m;

    public decimal Spread { get; init; }

    public CssColor Color { get; init; } = new("#00000033");
}
