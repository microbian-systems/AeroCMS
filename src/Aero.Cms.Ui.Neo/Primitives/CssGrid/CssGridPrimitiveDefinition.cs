using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.CssGrid;

public sealed class CssGridPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
    public const string ContentDropZone = "content";

    private static readonly IReadOnlySet<NeoPageNodeKind> ChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Block,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new CssGridPrimitiveDefinition(), new CssGridPrimitiveDefinition());

    public override string CatalogId => "primitive.css-grid";
    public override string DisplayName => "CSS Grid";
    public override string? Description => "A direct CSS grid container that accepts primitives, containers, and components.";
    public override string Category => "Primitives";
    public override string IconName => "layout-grid";
    public override int SortOrder => 4;
    public override Type? PreviewComponentType => typeof(CssGridPrimitivePreview);
    public override Type? PropertyEditorComponentType => null;

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Container(
            ChildKinds,
            [
                NeoPageNodeKind.Section,
                NeoPageNodeKind.Container,
                NeoPageNodeKind.Component
            ],
            dropZones:
            [
                new NeoDropZoneDefinition(ContentDropZone, ChildKinds)
            ]);

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Layout |
        EditorCapabilitySet.Alignment |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
        EditorCapabilitySet.Visibility |
        EditorCapabilitySet.Direction;

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
                ["columns"] = JsonSerializer.SerializeToElement(3),
                ["gap"] = JsonSerializer.SerializeToElement(4)
            }
        };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;

    private static readonly IReadOnlyList<ISlotDefinition> _slots =
    [
        new SlotDefinition(
            Id: ContentDropZone,
            DisplayName: "Grid Items",
            AllowedChildKinds: new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Primitive,
                NeoPageNodeKind.Container,
                NeoPageNodeKind.Component
            })
    ];

    public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(slot => slot.Id == slotId);
}
