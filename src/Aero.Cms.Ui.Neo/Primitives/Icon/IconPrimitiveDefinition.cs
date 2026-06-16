using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Icon;

public sealed class IconPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new IconPrimitiveDefinition(), new IconPrimitiveDefinition());

    public override string CatalogId => "primitive.icon";
    public override string DisplayName => "Icon";
    public override string? Description => "A Lucide icon with an accessible label.";
    public override string Category => "Primitives";
    public override string IconName => "circle";
    public override int SortOrder => 50;
    public override Type? PreviewComponentType => typeof(IconPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(IconPrimitiveEditor);
    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Icon |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Direction |
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
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("sparkles"),
                ["label"] = JsonSerializer.SerializeToElement("Sparkles")
            }
        };
}
