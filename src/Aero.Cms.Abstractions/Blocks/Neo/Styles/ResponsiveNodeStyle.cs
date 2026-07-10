namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Responsive style values with Desktop to Tablet to Mobile inheritance.
/// </summary>
public sealed class ResponsiveNodeStyle
{
        /// <summary>
    /// Gets or sets the Base.
    /// </summary>
public NodeStyle Base { get; set; } = new();

        /// <summary>
    /// Gets or sets the Tablet.
    /// </summary>
public NodeStyleOverride? Tablet { get; set; }

        /// <summary>
    /// Gets or sets the Mobile.
    /// </summary>
public NodeStyleOverride? Mobile { get; set; }

        /// <summary>
    /// Resolve method.
    /// </summary>
public NodeStyle Resolve(EditorBreakpoint breakpoint) =>
        breakpoint switch
        {
            EditorBreakpoint.Desktop => Base,
            EditorBreakpoint.Tablet => Base.Apply(Tablet),
            EditorBreakpoint.Mobile => Base.Apply(Tablet).Apply(Mobile),
            _ => throw new ArgumentOutOfRangeException(nameof(breakpoint), breakpoint, null)
        };

        /// <summary>
    /// DeepClone method.
    /// </summary>
public ResponsiveNodeStyle DeepClone() =>
        new()
        {
            Base = Base with
            {
                Margin = Base.Margin with { },
                Padding = Base.Padding with { }
            },
            Tablet = Clone(Tablet),
            Mobile = Clone(Mobile)
        };

    private static NodeStyleOverride? Clone(NodeStyleOverride? value) =>
        value is null
            ? null
            : value with
            {
                Margin = value.Margin is null ? null : value.Margin with { },
                Padding = value.Padding is null ? null : value.Padding with { }
            };
}
