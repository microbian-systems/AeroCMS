using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Collections.Generic;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Grid;

public sealed class GridRowDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlySet<NeoPageNodeKind> RowChildKinds =
        new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Container };

    private static readonly IReadOnlySet<NeoPageNodeKind> RowParentKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Container
        };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new GridRowDefinition(), new GridRowDefinition());

    public override string CatalogId => "primitive.grid-row";
    public override string DisplayName => "Grid Row";
    public override string? Description => "A row inside a grid. Contains grid cells.";
    public override string Category => "Primitives";
    public override string IconName => "rows-2";
    public override int SortOrder => 2;
    public override Type? PreviewComponentType => typeof(GridRowPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(GridRowPrimitiveEditor);

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Container(
            RowChildKinds,
            RowParentKinds,
            dropZones:
            [
                new NeoDropZoneDefinition("grid-cells", RowChildKinds)
            ],
            allowedParentCatalogIds: new HashSet<string> { "primitive.grid" });

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Alignment |
        EditorCapabilitySet.Visibility;

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable |
        EditorInteractionCapabilities.PasteTarget;

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

    public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
