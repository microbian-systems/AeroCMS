using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Button;

public sealed class ButtonPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new ButtonPrimitiveDefinition(), new ButtonPrimitiveDefinition());

    public override string CatalogId => "primitive.button";
    public override string DisplayName => "Button";
    public override string? Description => "A responsive linked action.";
    public override string Category => "Primitives";
    public override string IconName => "mouse-pointer-click";
    public override int SortOrder => 20;
    public override bool PublicStaticSsrSafe => true;
    public override Type? PreviewComponentType => typeof(ButtonPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(ButtonPrimitiveEditor);
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
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
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
                ["text"] = JsonSerializer.SerializeToElement("Button"),
                ["url"] = JsonSerializer.SerializeToElement("#")
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
