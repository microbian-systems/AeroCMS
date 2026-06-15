using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Container;

public sealed class ContainerPrimitiveDefinition :
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
        new(new ContainerPrimitiveDefinition(), new ContainerPrimitiveDefinition());

    public string CatalogId => "primitive.container";
    public string DisplayName => "Container";
    public string? Description => "A responsive container for primitives and components.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Container;
    public string IconName => "square-dashed";
    public int SortOrder => 1;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(ContainerPrimitivePreview);
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
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["layout"] = JsonSerializer.SerializeToElement("stack"),
                ["gap"] = JsonSerializer.SerializeToElement(4)
            }
        };
}
