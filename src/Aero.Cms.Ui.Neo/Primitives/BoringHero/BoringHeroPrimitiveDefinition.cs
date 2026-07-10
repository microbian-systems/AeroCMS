using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.BoringHero;

/// <summary>
/// Represents a class for BoringHeroPrimitiveDefinition.
/// </summary>
public sealed class BoringHeroPrimitiveDefinition : PrimitiveDefinitionBase
{
        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new BoringHeroPrimitiveDefinition(), new BoringHeroPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "boring_hero";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Boring Hero";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Simple hero section with title, subtitle, and background.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Components";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "layout";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 91;
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
        EditorInteractionCapabilities.Selectable
        | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable
        | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable
        | EditorInteractionCapabilities.Copyable;

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content
        | EditorCapabilitySet.Spacing
        | EditorCapabilitySet.Dimensions
        | EditorCapabilitySet.Layout
        | EditorCapabilitySet.Alignment
        | EditorCapabilitySet.Background
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
            ["title"] = JsonSerializer.SerializeToElement("Welcome"),
            ["summary"] = JsonSerializer.SerializeToElement("Build a polished page with Aero CMS."),
            ["backgroundImageUrl"] = JsonSerializer.SerializeToElement(string.Empty)
        }
    };
}
