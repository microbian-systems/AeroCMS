namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Direction-safe spacing expressed with CSS logical axes.
/// </summary>
public sealed record LogicalSpacing
{
    public CssLength? BlockStart { get; init; }

    public CssLength? BlockEnd { get; init; }

    public CssLength? InlineStart { get; init; }

    public CssLength? InlineEnd { get; init; }

    internal LogicalSpacing Apply(LogicalSpacingOverride? value) =>
        value is null
            ? this
            : this with
            {
                BlockStart = value.BlockStart ?? BlockStart,
                BlockEnd = value.BlockEnd ?? BlockEnd,
                InlineStart = value.InlineStart ?? InlineStart,
                InlineEnd = value.InlineEnd ?? InlineEnd
            };
}

/// <summary>
/// Optional logical-spacing values applied at a responsive breakpoint.
/// </summary>
public sealed record LogicalSpacingOverride
{
    public CssLength? BlockStart { get; init; }

    public CssLength? BlockEnd { get; init; }

    public CssLength? InlineStart { get; init; }

    public CssLength? InlineEnd { get; init; }
}
