using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Ui.Neo.Primitives.Separator;

public sealed class SeparatorPrimitiveDefinition :
    IPageEditorCatalogDefinition,
    INeoNodeFactory,
    IEmbeddable
{
    public static PageEditorDefinitionDescriptor Descriptor { get; } =
        new(new SeparatorPrimitiveDefinition(), new SeparatorPrimitiveDefinition());

    public string CatalogId => "primitive.separator";
    public string DisplayName => "Separator";
    public string? Description => "A visual divider between content.";
    public string Category => "Primitives";
    public NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
    public string IconName => "minus";
    public int SortOrder => 60;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(SeparatorPrimitivePreview);
    public Type? PropertyEditorComponentType => null;
    public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Visibility;

    public NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind
        };
}
