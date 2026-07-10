using System.Linq;
using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Hero;

/// <summary>
/// Represents a class for HeroPrimitiveDefinition.
/// </summary>
public sealed class HeroPrimitiveDefinition : ContainerDefinitionBase, ISlotted
{
    private static readonly IReadOnlySet<NeoPageNodeKind> ChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
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

    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "background",
            DisplayName: "Background",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 1
        ),
        new SlotDefinition(
            Id: "title",
            DisplayName: "Title",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 1
        ),
        new SlotDefinition(
            Id: "subtitle",
            DisplayName: "Subtitle",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 1
        ),
        new SlotDefinition(
            Id: "cta",
            DisplayName: "Call to Action",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 2
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
        new(new HeroPrimitiveDefinition(), new HeroPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public override string CatalogId => "hero";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public override string DisplayName => "Hero";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string? Description => "Full-width hero section with background media, headline, subtitle, and call-to-action buttons.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override string Category => "Components";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public override string IconName => "layout";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public override int SortOrder => 200;
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public override NeoPageNodeKind Kind => NeoPageNodeKind.Component;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public override Type? PreviewComponentType => null; // TODO: add preview later
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public override Type? PropertyEditorComponentType => null; // TODO: add editor later

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
