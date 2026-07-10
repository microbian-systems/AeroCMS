using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Audio;

/// <summary>
/// Represents a class for AudioPrimitiveDefinition.
/// </summary>
public sealed class AudioPrimitiveDefinition : PrimitiveDefinitionBase
{
        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new AudioPrimitiveDefinition(), new AudioPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "audio";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Audio";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Embed an audio player with controls.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "volume-2";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 95;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => null;
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public override Type? PropertyEditorComponentType => null;

        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);

        /// <summary>
    /// Gets or sets the Interaction.
    /// </summary>
public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable
        | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable
        | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable
        | EditorInteractionCapabilities.Copyable
        | EditorInteractionCapabilities.MediaSelectable;

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content
        | EditorCapabilitySet.Media
        | EditorCapabilitySet.Spacing
        | EditorCapabilitySet.Dimensions
        | EditorCapabilitySet.Visibility;

        /// <summary>
    /// CreateDefaultNode method.
    /// </summary>
public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>
        {
            ["url"] = JsonSerializer.SerializeToElement(string.Empty),
            ["caption"] = JsonSerializer.SerializeToElement("Select an audio file")
        }
    };
}
