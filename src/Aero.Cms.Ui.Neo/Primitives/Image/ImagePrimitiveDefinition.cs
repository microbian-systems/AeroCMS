using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Image;

public sealed class ImagePrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new ImagePrimitiveDefinition(), new ImagePrimitiveDefinition());

    public override string CatalogId => "primitive.image";
    public override string DisplayName => "Image";
    public override string? Description => "A responsive image with alternative text and caption.";
    public override string Category => "Primitives";
    public override string IconName => "image";
    public override int SortOrder => 30;
    public override bool PublicStaticSsrSafe => true;
    public override Type? PreviewComponentType => typeof(ImagePrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(ImagePrimitiveEditor);
    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Media |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
        EditorCapabilitySet.Visibility |
        EditorCapabilitySet.Direction;

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable |
        EditorInteractionCapabilities.MediaSelectable;

    public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["url"] = JsonSerializer.SerializeToElement(string.Empty),
                ["alt"] = JsonSerializer.SerializeToElement(""),
                ["caption"] = JsonSerializer.SerializeToElement("")
            }
        };
}
