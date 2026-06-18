using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Columns;

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

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new ColumnsPrimitiveDefinition(), new ColumnsPrimitiveDefinition());

    public override string CatalogId => "columns";
    public override string DisplayName => "Columns";
    public override string? Description => "Multi-column layout container.";
    public override string Category => "Primitives";
    public override string IconName => "view_column";
    public override int SortOrder => 102;

    public override Type? PreviewComponentType => null;
    public override Type? PropertyEditorComponentType => null;

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

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable | EditorInteractionCapabilities.Copyable
        | EditorInteractionCapabilities.PasteTarget;

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Layout | EditorCapabilitySet.Spacing | EditorCapabilitySet.Visibility;

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _columnsSlots;

    public ISlotDefinition? GetSlot(string slotId) =>
        _columnsSlots.FirstOrDefault(s => s.Id == slotId);

    public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>(),
        Children = []
    };
}
