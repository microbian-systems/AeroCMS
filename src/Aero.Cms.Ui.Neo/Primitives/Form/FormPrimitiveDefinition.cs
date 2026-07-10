using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Form;

/// <summary>
/// Represents a class for FormPrimitiveDefinition.
/// </summary>
public sealed class FormPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
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
        new(new FormPrimitiveDefinition(), new FormPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "primitive.form";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Form";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Semantic form container for inputs and controls.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "form-input";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 10;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => typeof(FormPrimitivePreview);
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
                new NeoDropZoneDefinition(NeoDropZoneDefinition.DefaultId, ChildKinds)
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
            Properties = new Dictionary<string, JsonElement>()
        };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;
    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "default",
            DisplayName: "Form Controls",
            AllowedChildKinds: new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Primitive, NeoPageNodeKind.Block
            },
            MinChildren: 0),
    };

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
