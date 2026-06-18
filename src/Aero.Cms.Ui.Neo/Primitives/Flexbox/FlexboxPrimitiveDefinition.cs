using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Flexbox;

public sealed class FlexboxPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
    public const string ContentDropZone = "content";

    private static readonly IReadOnlySet<NeoPageNodeKind> ChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new FlexboxPrimitiveDefinition(), new FlexboxPrimitiveDefinition());

    public override string CatalogId => "primitive.flexbox";
    public override string DisplayName => "Flexbox";
    public override string? Description => "A flexible row or column container for composing responsive layouts.";
    public override string Category => "Primitives";
    public override string IconName => "panel-top";
    public override int SortOrder => 3;
    public override Type? PreviewComponentType => typeof(FlexboxPrimitivePreview);
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
                ["direction"] = JsonSerializer.SerializeToElement("row"),
                ["wrap"] = JsonSerializer.SerializeToElement(true),
                ["gap"] = JsonSerializer.SerializeToElement(4),
                ["justify"] = JsonSerializer.SerializeToElement("start"),
                ["align"] = JsonSerializer.SerializeToElement("stretch")
            }
        };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;

    private static readonly IReadOnlyList<ISlotDefinition> _slots =
    [
        new SlotDefinition(
            Id: ContentDropZone,
            DisplayName: "Flex Items",
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
