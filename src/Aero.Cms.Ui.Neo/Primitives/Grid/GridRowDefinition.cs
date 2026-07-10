using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Collections.Generic;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Grid;

/// <summary>
/// Represents a class for GridRowDefinition.
/// </summary>
public sealed class GridRowDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlySet<NeoPageNodeKind> RowChildKinds =
        new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Container };

    private static readonly IReadOnlySet<NeoPageNodeKind> RowParentKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Container
        };

        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new GridRowDefinition(), new GridRowDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "primitive.grid-row";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Grid Row";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "A row inside a grid. Contains grid cells.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "rows-2";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 2;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => typeof(GridRowPrimitivePreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public override Type? PropertyEditorComponentType => typeof(GridRowPrimitiveEditor);

        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Container(
            RowChildKinds,
            RowParentKinds,
            dropZones:
            [
                new NeoDropZoneDefinition("grid-cells", RowChildKinds)
            ],
            allowedParentCatalogIds: new HashSet<string> { "primitive.grid" });

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Alignment |
        EditorCapabilitySet.Visibility;

        /// <summary>
    /// Gets or sets the Interaction.
    /// </summary>
public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable |
        EditorInteractionCapabilities.PasteTarget;

        /// <summary>
    /// CreateDefaultNode method.
    /// </summary>
public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["gap"] = JsonSerializer.SerializeToElement(4)
            }
        };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;
    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "cells",
            DisplayName: "Grid Cells",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Block },
            MaxChildren: 12
        ),
    };

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
