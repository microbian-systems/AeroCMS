using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.CssGrid;

/// <summary>
/// Represents a class for CssGridPrimitiveDefinition.
/// </summary>
public sealed class CssGridPrimitiveDefinition : ContainerDefinitionBase, ISlotted
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
        new(new CssGridPrimitiveDefinition(), new CssGridPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "primitive.css-grid";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "CSS Grid";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "A direct CSS grid container that accepts primitives, containers, and components.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "layout-grid";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 4;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => typeof(CssGridPrimitivePreview);
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

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(slot => slot.Id == slotId);
}
