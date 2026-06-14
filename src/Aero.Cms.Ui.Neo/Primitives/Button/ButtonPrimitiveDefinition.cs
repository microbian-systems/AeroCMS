using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Button;

public sealed class ButtonPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new ButtonPrimitiveDefinition(), new ButtonPrimitiveDefinition());

    public string CatalogId => "primitive.button";
    public string DisplayName => "Button";
    public string? Description => "A responsive linked action.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
    public string IconName => "mouse-pointer-click";
    public int SortOrder => 20;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(ButtonPrimitivePreview);
    public Type? PropertyEditorComponentType => typeof(ButtonPrimitiveEditor);
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
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
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
                ["text"] = JsonSerializer.SerializeToElement("Button"),
                ["url"] = JsonSerializer.SerializeToElement("#")
            }
        };
}
