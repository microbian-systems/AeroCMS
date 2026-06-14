using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Text;

public sealed class TextPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new TextPrimitiveDefinition(), new TextPrimitiveDefinition());

    public string CatalogId => "primitive.text";
    public string DisplayName => "Text";
    public string? Description => "Responsive body text.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
    public string IconName => "type";
    public int SortOrder => 10;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(TextPrimitivePreview);
    public Type? PropertyEditorComponentType => typeof(TextPrimitiveEditor);
    public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Typography |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Direction |
        EditorCapabilitySet.Visibility;

    public NeoPageNode CreateDefaultNode() =>
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
}
