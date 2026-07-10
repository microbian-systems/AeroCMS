namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Direction-safe spacing expressed with CSS logical axes.
/// </summary>
public sealed record LogicalSpacing
{
        /// <summary>
    /// Gets or sets the Block Start.
    /// </summary>
public CssLength? BlockStart { get; init; }

        /// <summary>
    /// Gets or sets the Block End.
    /// </summary>
public CssLength? BlockEnd { get; init; }

        /// <summary>
    /// Gets or sets the Inline Start.
    /// </summary>
public CssLength? InlineStart { get; init; }

        /// <summary>
    /// Gets or sets the Inline End.
    /// </summary>
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
        /// <summary>
    /// Gets or sets the Block Start.
    /// </summary>
public CssLength? BlockStart { get; init; }

        /// <summary>
    /// Gets or sets the Block End.
    /// </summary>
public CssLength? BlockEnd { get; init; }

        /// <summary>
    /// Gets or sets the Inline Start.
    /// </summary>
public CssLength? InlineStart { get; init; }

        /// <summary>
    /// Gets or sets the Inline End.
    /// </summary>
public CssLength? InlineEnd { get; init; }
}
