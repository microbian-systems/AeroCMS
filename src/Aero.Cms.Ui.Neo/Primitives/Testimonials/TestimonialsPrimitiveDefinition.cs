using System.Linq;
using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Testimonials;

/// <summary>
/// Represents a class for TestimonialsPrimitiveDefinition.
/// </summary>
public sealed class TestimonialsPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlySet<NeoPageNodeKind> ChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Block,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    private static readonly IReadOnlySet<NeoPageNodeKind> ParentKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    private static readonly IReadOnlySet<NeoPageNodeKind> ItemsChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Block,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "title",
            DisplayName: "Title",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 1
        ),
        new SlotDefinition(
            Id: "description",
            DisplayName: "Description",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 1
        ),
        new SlotDefinition(
            Id: "testimonials",
            DisplayName: "Testimonials",
            AllowedChildKinds: ItemsChildKinds,
            MinChildren: 1,
            MaxChildren: 20
        ),
        new SlotDefinition(
            Id: "content",
            DisplayName: "Content",
            AllowedChildKinds: ChildKinds,
            MinChildren: 0
        ),
    };

        /// <summary>
    /// Gets or sets the Descriptor.
    /// </summary>
public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new TestimonialsPrimitiveDefinition(), new TestimonialsPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "aero_testimonials";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Testimonials";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Customer testimonials section with quote cards.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Components";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "quote";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 81;
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public override NeoPageNodeKind Kind => NeoPageNodeKind.Component;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => null;
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
            ParentKinds,
            dropZones: _slots.Select(s =>
                new NeoDropZoneDefinition(s.Id, s.AllowedChildKinds, s.MaxChildren)).ToArray(),
            isSlotted: true);

        /// <summary>
    /// Gets or sets the Interaction.
    /// </summary>
public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable
        | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable
        | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable
        | EditorInteractionCapabilities.Copyable
        | EditorInteractionCapabilities.PasteTarget;

        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing
        | EditorCapabilitySet.Dimensions
        | EditorCapabilitySet.Layout
        | EditorCapabilitySet.Alignment
        | EditorCapabilitySet.Background
        | EditorCapabilitySet.Border
        | EditorCapabilitySet.Effects
        | EditorCapabilitySet.Visibility
        | EditorCapabilitySet.Direction;

        /// <summary>
    /// CreateDefaultNode method.
    /// </summary>
public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(DisplayName),
            ["description"] = JsonSerializer.SerializeToElement(Description ?? string.Empty)
        },
        Children = new List<NeoPageNode>()
    };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
