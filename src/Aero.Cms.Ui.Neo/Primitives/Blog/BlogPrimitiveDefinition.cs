using System.Linq;
using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Blog;

public sealed class BlogPrimitiveDefinition : ContainerDefinitionBase, ISlotted
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

    private static readonly IReadOnlySet<NeoPageNodeKind> PostsChildKinds =
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
            Id: "posts",
            DisplayName: "Posts",
            AllowedChildKinds: PostsChildKinds,
            MinChildren: 1,
            MaxChildren: 20
        ),
    };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new BlogPrimitiveDefinition(), new BlogPrimitiveDefinition());

    public override string CatalogId => "aero_blog";
    public override string DisplayName => "Blog";
    public override string? Description => "Blog posts section with article cards.";
    public override string Category => "Components";
    public override string IconName => "notebook-text";
    public override int SortOrder => 82;
    public override NeoPageNodeKind Kind => NeoPageNodeKind.Component;
    public override Type? PreviewComponentType => null;
    public override Type? PropertyEditorComponentType => null;

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Container(
            ChildKinds,
            ParentKinds,
            dropZones: _slots.Select(s =>
                new NeoDropZoneDefinition(s.Id, s.AllowedChildKinds, s.MaxChildren)).ToArray(),
            isSlotted: true);

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable
        | EditorInteractionCapabilities.Editable
        | EditorInteractionCapabilities.Draggable
        | EditorInteractionCapabilities.Duplicatable
        | EditorInteractionCapabilities.Deletable
        | EditorInteractionCapabilities.Copyable
        | EditorInteractionCapabilities.PasteTarget;

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

    public override NeoPageNode CreateDefaultNode() => new()
    {
        NodeId = Guid.NewGuid().ToString("N"),
        CatalogId = CatalogId,
        Kind = Kind,
        Properties = new Dictionary<string, JsonElement>(),
        Children = new List<NeoPageNode>()
    };

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;

    public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
