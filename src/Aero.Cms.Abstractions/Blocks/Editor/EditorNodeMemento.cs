using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Captures an isolated snapshot of a node subtree for modal editing and history.
/// </summary>
public sealed class EditorNodeMemento
{
    private readonly NeoPageNode _snapshot;

    private EditorNodeMemento(NeoPageNode snapshot)
    {
        _snapshot = snapshot;
    }

    public static EditorNodeMemento Capture(NeoPageNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new EditorNodeMemento(Clone(node));
    }

    public NeoPageNode Restore() => Clone(_snapshot);

    internal static NeoPageNode Clone(NeoPageNode node) =>
        new()
        {
            NodeId = node.NodeId,
            CatalogId = node.CatalogId,
            Kind = node.Kind,
            Properties = node.Properties.ToDictionary(
                property => property.Key,
                property => property.Value.Clone(),
                StringComparer.Ordinal),
            Style = Clone(node.Style),
            Children = node.Children.Select(Clone).ToList()
        };

    private static ResponsiveNodeStyle Clone(ResponsiveNodeStyle style) =>
        new()
        {
            Base = Clone(style.Base),
            Tablet = Clone(style.Tablet),
            Mobile = Clone(style.Mobile)
        };

    private static NodeStyle Clone(NodeStyle style) =>
        style with
        {
            Margin = style.Margin with { },
            Padding = style.Padding with { }
        };

    private static NodeStyleOverride? Clone(NodeStyleOverride? style) =>
        style is null
            ? null
            : style with
            {
                Margin = style.Margin is null ? null : style.Margin with { },
                Padding = style.Padding is null ? null : style.Padding with { }
            };
}
