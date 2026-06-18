using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Aside;

public sealed class AsidePrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlySet<NeoPageNodeKind> ChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new AsidePrimitiveDefinition(), new AsidePrimitiveDefinition());

    public override string CatalogId => "primitive.aside";
    public override string DisplayName => "Aside";
    public override string? Description => "Semantic sidebar or complementary content container.";
    public override string Category => "Primitives";
    public override string IconName => "panel-right";
    public override int SortOrder => 6;
    public override Type? PreviewComponentType => typeof(AsidePrimitivePreview);
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
                new NeoDropZoneDefinition(NeoDropZoneDefinition.DefaultId, ChildKinds)
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
            Properties = new Dictionary<string, JsonElement>()
        };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;
    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "default",
            DisplayName: "Aside Content",
            AllowedChildKinds: new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Primitive, NeoPageNodeKind.Block,
                NeoPageNodeKind.Container, NeoPageNodeKind.Component
            },
            MinChildren: 0),
    };

    public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
