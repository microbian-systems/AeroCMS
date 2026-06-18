using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.DynamicTemplate;

public sealed class DynamicTemplatePrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new DynamicTemplatePrimitiveDefinition(), new DynamicTemplatePrimitiveDefinition());

    public override string CatalogId => "dynamic_template";
    public override string DisplayName => "Dynamic Template";
    public override string? Description => "Scriban template block for dynamic content.";
    public override string Category => "Primitives";
    public override string IconName => "dynamic_form";
    public override int SortOrder => 101;

    public override Type? PreviewComponentType => null;
    public override Type? PropertyEditorComponentType => null;

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component,
            NeoPageNodeKind.Block);

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable | EditorInteractionCapabilities.Copyable;

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content | EditorCapabilitySet.Spacing | EditorCapabilitySet.Visibility;

    public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>()
    };
}
