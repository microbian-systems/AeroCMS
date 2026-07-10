using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Columns;

/// <summary>
/// Represents a class for ColumnsPrimitiveDefinition.
/// </summary>
public sealed class ColumnsPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlyList<ISlotDefinition> _columnsSlots = new[]
    {
        new SlotDefinition(
            "columns",
            "Columns",
            new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Primitive, NeoPageNodeKind.Block,
                NeoPageNodeKind.Container, NeoPageNodeKind.Component
            },
            MinChildren: 1,
            MaxChildren: 6)
    };

        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new ColumnsPrimitiveDefinition(), new ColumnsPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "columns";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Columns";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Multi-column layout container.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "view_column";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 102;

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
        CompositionCapabilities.Container(
            new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Primitive, NeoPageNodeKind.Block,
                NeoPageNodeKind.Container, NeoPageNodeKind.Component
            },
            new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Section, NeoPageNodeKind.Container,
                NeoPageNodeKind.Component, NeoPageNodeKind.Block
            },
            dropZones: _columnsSlots.Select(s =>
                new NeoDropZoneDefinition(s.Id, s.AllowedChildKinds, s.MaxChildren)).ToArray(),
            isSlotted: true);

        /// <summary>
    /// Gets or sets the Interaction.
    /// </summary>
public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable | EditorInteractionCapabilities.Copyable
        | EditorInteractionCapabilities.PasteTarget;

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Layout | EditorCapabilitySet.Spacing | EditorCapabilitySet.Visibility;

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _columnsSlots;

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _columnsSlots.FirstOrDefault(s => s.Id == slotId);

        /// <summary>
    /// CreateDefaultNode method.
    /// </summary>
public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>(),
        Children = []
    };
}
