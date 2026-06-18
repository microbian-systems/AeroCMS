using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using System.Collections.Generic;
using System.Text.Json;

namespace Aero.Cms.Ui.Neo.Primitives.Grid;

public sealed class GridCellDefinition : ContainerDefinitionBase
{
    private static readonly IReadOnlySet<NeoPageNodeKind> CellChildKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Primitive,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component
        };

    private static readonly IReadOnlySet<NeoPageNodeKind> CellParentKinds =
        new HashSet<NeoPageNodeKind>
        {
            NeoPageNodeKind.Container
        };

    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new GridCellDefinition(), new GridCellDefinition());

    public override string CatalogId => "primitive.grid-cell";
    public override string DisplayName => "Grid Cell";
    public override string? Description => "A cell inside a grid row. Accepts primitives, containers, and components.";
    public override string Category => "Primitives";
    public override string IconName => "grid-2x2";
    public override int SortOrder => 3;
    public override Type? PreviewComponentType => typeof(GridCellPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(GridCellPrimitiveEditor);

    public override ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Container(
            CellChildKinds,
            CellParentKinds,
            dropZones:
            [
                new NeoDropZoneDefinition("cell-content", CellChildKinds)
            ],
            allowedParentCatalogIds: new HashSet<string> { "primitive.grid-row" });

    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Layout |
        EditorCapabilitySet.Alignment |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Effects |
        EditorCapabilitySet.Visibility;

    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable |
        EditorInteractionCapabilities.PasteTarget;

    public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["span"] = JsonSerializer.SerializeToElement(6)
            }
        };
}
