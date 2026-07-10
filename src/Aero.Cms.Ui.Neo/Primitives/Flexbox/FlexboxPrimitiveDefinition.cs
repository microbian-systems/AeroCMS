using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Flexbox;

/// <summary>
/// Represents a class for FlexboxPrimitiveDefinition.
/// </summary>
public sealed class FlexboxPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
        /// <summary>
    /// ContentDropZone.
    /// </summary>
public const string ContentDropZone = "content";

    private static readonly IReadOnlySet<NeoPageNodeKind> ChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Block,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new FlexboxPrimitiveDefinition(), new FlexboxPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "primitive.flexbox";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Flexbox";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "A flexible row or column container for composing responsive layouts.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "panel-top";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 3;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => typeof(FlexboxPrimitivePreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public override Type? PropertyEditorComponentType => null;

        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
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

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
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

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(slot => slot.Id == slotId);
}
