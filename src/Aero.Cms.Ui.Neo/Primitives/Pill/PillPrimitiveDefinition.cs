using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Pill;

public sealed class PillPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new PillPrimitiveDefinition(), new PillPrimitiveDefinition());

    public override string CatalogId => "primitive.pill";
    public override string DisplayName => "Pill";
    public override string? Description => "A compact label or badge.";
    public override string Category => "Primitives";
    public override string IconName => "badge";
    public override int SortOrder => 40;
    public override bool PublicStaticSsrSafe => true;
    public override Type? PreviewComponentType => typeof(PillPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(PillPrimitiveEditor);
    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Typography |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
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
                ["text"] = JsonSerializer.SerializeToElement("Badge")
            }
        };
}
