using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Grid;

public sealed class GridDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlySet<NeoPageNodeKind> GridChildKinds =
        new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Container };

    private static readonly IReadOnlySet<NeoPageNodeKind> GridParentKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new GridDefinition(), new GridDefinition());

    public override string CatalogId => "primitive.grid";
    public override string DisplayName => "Grid";
    public override string? Description => "Responsive CSS grid layout container. Add rows, then cells.";
    public override string Category => "Primitives";
    public override string IconName => "layout-grid";
    public override int SortOrder => 1;
    public override Type? PreviewComponentType => typeof(GridPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(GridPrimitiveEditor);

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Container(
            GridChildKinds,
            GridParentKinds,
            dropZones:
            [
                new NeoDropZoneDefinition("grid-rows", GridChildKinds)
            ]);

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Layout |
        EditorCapabilitySet.Alignment |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
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
                ["columns"] = JsonSerializer.SerializeToElement(12),
                ["gap"] = JsonSerializer.SerializeToElement(4)
            }
        };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;
    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "rows",
            DisplayName: "Grid Rows",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Block },
            MinChildren: 1,
            MaxChildren: 100
        ),
    };

    public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
