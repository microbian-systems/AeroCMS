using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Image;

public sealed class ImagePrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory,
    IEmbeddable
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new ImagePrimitiveDefinition(), new ImagePrimitiveDefinition());

    public string CatalogId => "primitive.image";
    public string DisplayName => "Image";
    public string? Description => "A responsive image with alternative text and caption.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
    public string IconName => "image";
    public int SortOrder => 30;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(ImagePrimitivePreview);
    public Type? PropertyEditorComponentType => typeof(ImagePrimitiveEditor);
    public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Media |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
        EditorCapabilitySet.Visibility |
        EditorCapabilitySet.Direction;

    public NeoPageNode CreateDefaultNode() =>
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
