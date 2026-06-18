using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.MarkdownBlock;

public sealed class MarkdownBlockPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new MarkdownBlockPrimitiveDefinition(), new MarkdownBlockPrimitiveDefinition());

    public override string CatalogId => "markdown";
    public override string DisplayName => "Markdown";
    public override string? Description => "Markdown content block.";
    public override string Category => "Primitives";
    public override string IconName => "code";
    public override int SortOrder => 100;

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
