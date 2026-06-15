using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Card;

public sealed class CardPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory,
    IEmbeddable
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
        new(new CardPrimitiveDefinition(), new CardPrimitiveDefinition());

    public string CatalogId => "preset.card";
    public string DisplayName => "Card";
    public string? Description => "An editable card composed from standard primitives.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Component;
    public string IconName => "square-stack";
    public int SortOrder => 100;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(CardPrimitivePreview);
    public Type? PropertyEditorComponentType => null;
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
}
