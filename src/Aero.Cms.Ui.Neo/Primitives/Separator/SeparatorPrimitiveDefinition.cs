using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Separator;

public sealed class SeparatorPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new SeparatorPrimitiveDefinition(), new SeparatorPrimitiveDefinition());

    public override string CatalogId => "primitive.separator";
    public override string DisplayName => "Separator";
    public override string? Description => "A visual divider between content.";
    public override string Category => "Primitives";
    public override string IconName => "minus";
    public override int SortOrder => 60;
    public override Type? PreviewComponentType => typeof(SeparatorPrimitivePreview);
    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Visibility;

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable;

    public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind
        };
}
