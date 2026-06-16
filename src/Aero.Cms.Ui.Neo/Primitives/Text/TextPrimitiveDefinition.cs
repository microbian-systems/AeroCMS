using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Text;

public sealed class TextPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new TextPrimitiveDefinition(), new TextPrimitiveDefinition());

    public override string CatalogId => "primitive.text";
    public override string DisplayName => "Text";
    public override string? Description => "Responsive body text.";
    public override string Category => "Primitives";
    public override string IconName => "type";
    public override int SortOrder => 10;
    public override bool PublicStaticSsrSafe => true;
    public override Type? PreviewComponentType => typeof(TextPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(TextPrimitiveEditor);
    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Typography |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Direction |
        EditorCapabilitySet.Visibility;

    public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement("Enter your text here...")
            }
        };

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable;
}
