using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Embed;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Embed;

public sealed class EmbedPrimitiveDefinition : PrimitiveDefinitionBase
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new EmbedPrimitiveDefinition(), new EmbedPrimitiveDefinition());

    public override string CatalogId => "primitive.embed";
    public override string DisplayName => "Embed";
    public override string? Description => "Embed external content (YouTube, Vimeo, Maps, etc.) via a secure iframe.";
    public override string Category => "Primitives";
    public override string IconName => "code-xml";
    public override int SortOrder => 40;
    public override bool PublicStaticSsrSafe => true;
    public override Type? PreviewComponentType => typeof(EmbedPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(EmbedPrimitiveEditor);

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Visibility;

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable;

    public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["url"] = JsonSerializer.SerializeToElement(""),
                ["aspectRatio"] = JsonSerializer.SerializeToElement("widescreen"),
                ["sandbox"] = JsonSerializer.SerializeToElement("video"),
                ["title"] = JsonSerializer.SerializeToElement("Embedded content")
            }
        };
}
