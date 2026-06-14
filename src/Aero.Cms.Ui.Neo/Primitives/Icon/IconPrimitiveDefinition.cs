using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Icon;

public sealed class IconPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new IconPrimitiveDefinition(), new IconPrimitiveDefinition());

    public string CatalogId => "primitive.icon";
    public string DisplayName => "Icon";
    public string? Description => "A Lucide icon with an accessible label.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
    public string IconName => "circle";
    public int SortOrder => 50;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(IconPrimitivePreview);
    public Type? PropertyEditorComponentType => typeof(IconPrimitiveEditor);
    public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Icon |
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
                ["name"] = JsonSerializer.SerializeToElement("sparkles"),
                ["label"] = JsonSerializer.SerializeToElement("Sparkles")
            }
        };
}
