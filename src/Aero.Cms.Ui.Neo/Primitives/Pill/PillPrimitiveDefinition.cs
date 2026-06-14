using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Pill;

public sealed class PillPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new PillPrimitiveDefinition(), new PillPrimitiveDefinition());

    public string CatalogId => "primitive.pill";
    public string DisplayName => "Pill";
    public string? Description => "A compact label or badge.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
    public string IconName => "badge";
    public int SortOrder => 40;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(PillPrimitivePreview);
    public Type? PropertyEditorComponentType => typeof(PillPrimitiveEditor);
    public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Typography |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
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
                ["text"] = JsonSerializer.SerializeToElement("Badge")
            }
        };
}
