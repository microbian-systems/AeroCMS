using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Video;

public sealed class VideoPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new VideoPrimitiveDefinition(), new VideoPrimitiveDefinition());

    public override string CatalogId => "video";
    public override string DisplayName => "Video";
    public override string? Description => "Embed a video with playback options.";
    public override string Category => "Primitives";
    public override string IconName => "video";
    public override int SortOrder => 94;
    public override Type? PreviewComponentType => null;
    public override Type? PropertyEditorComponentType => null;

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable
        | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable
        | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable
        | EditorInteractionCapabilities.Copyable;

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content
        | EditorCapabilitySet.Media
        | EditorCapabilitySet.Spacing
        | EditorCapabilitySet.Dimensions
        | EditorCapabilitySet.Visibility;

    public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>()
    };
}
