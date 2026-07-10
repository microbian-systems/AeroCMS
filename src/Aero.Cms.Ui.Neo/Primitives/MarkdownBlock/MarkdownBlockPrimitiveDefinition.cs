using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.MarkdownBlock;

/// <summary>
/// Represents a class for MarkdownBlockPrimitiveDefinition.
/// </summary>
public sealed class MarkdownBlockPrimitiveDefinition : PrimitiveDefinitionBase
{
        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new MarkdownBlockPrimitiveDefinition(), new MarkdownBlockPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "markdown";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Markdown";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Markdown content block.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "code";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 100;

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
            NeoPageNodeKind.Component,
            NeoPageNodeKind.Block);

        /// <summary>
    /// Gets or sets the Interaction.
    /// </summary>
public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable | EditorInteractionCapabilities.Copyable;

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content | EditorCapabilitySet.Spacing | EditorCapabilitySet.Visibility;

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
            ["content"] = JsonSerializer.SerializeToElement("### Markdown\n\nWrite markdown content here.")
        }
    };
}
