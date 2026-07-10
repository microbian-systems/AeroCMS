using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Card;

/// <summary>
/// Represents a class for CardPrimitiveDefinition.
/// </summary>
public sealed class CardPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory,
    IEmbeddable,
    ISlotted
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
        new(new CardPrimitiveDefinition(), new CardPrimitiveDefinition());

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "preset.card";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Card";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "An editable card composed from standard primitives.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Primitives";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public NeoPageNodeKind Kind => NeoPageNodeKind.Component;
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "square-stack";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 100;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(CardPrimitivePreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => null;
        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
public ICompositionCapabilities Composition { get; } =
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
public EditorCapabilitySet EditorCapabilities =>
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
    /// CreateDefaultNode method.
    /// </summary>
public NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = NewId(),
            CatalogId = CatalogId,
            Kind = Kind,
            Children =
            [
                CreateNode("primitive.image", new()
                {
                    ["url"] = JsonSerializer.SerializeToElement(string.Empty),
                    ["alt"] = JsonSerializer.SerializeToElement(""),
                    ["caption"] = JsonSerializer.SerializeToElement("")
                }),
                CreateNode("primitive.pill", new()
                {
                    ["text"] = JsonSerializer.SerializeToElement("Featured")
                }),
                CreateNode("primitive.text", new()
                {
                    ["text"] = JsonSerializer.SerializeToElement("Card title")
                }),
                CreateNode("primitive.text", new()
                {
                    ["text"] = JsonSerializer.SerializeToElement(
                        "Add a concise description for this card.")
                }),
                CreateNode("primitive.button", new()
                {
                    ["text"] = JsonSerializer.SerializeToElement("Learn more"),
                    ["url"] = JsonSerializer.SerializeToElement("#")
                })
            ]
        };

    private static NeoPageNode CreateNode(
        string catalogId,
        Dictionary<string, JsonElement> properties) =>
        new()
        {
            NodeId = NewId(),
            CatalogId = catalogId,
            Kind = NeoPageNodeKind.Primitive,
            Properties = properties
        };

    private static string NewId() => Guid.NewGuid().ToString("N");

    IReadOnlyList<ISlotDefinition> ISlotted.Slots => _slots;
    private static readonly IReadOnlyList<ISlotDefinition> _slots = new[]
    {
        new SlotDefinition(
            Id: "media",
            DisplayName: "Media",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 1
        ),
        new SlotDefinition(
            Id: "content",
            DisplayName: "Content",
            AllowedChildKinds: new HashSet<NeoPageNodeKind>
            {
                NeoPageNodeKind.Primitive, NeoPageNodeKind.Block,
                NeoPageNodeKind.Container, NeoPageNodeKind.Component
            },
            MinChildren: 1
        ),
        new SlotDefinition(
            Id: "actions",
            DisplayName: "Actions",
            AllowedChildKinds: new HashSet<NeoPageNodeKind> { NeoPageNodeKind.Primitive },
            MaxChildren: 3
        ),
    };

        /// <summary>
    /// GetSlot method.
    /// </summary>
public ISlotDefinition? GetSlot(string slotId) =>
        _slots.FirstOrDefault(s => s.Id == slotId);
}
