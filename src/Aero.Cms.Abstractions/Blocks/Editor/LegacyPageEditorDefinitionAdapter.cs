using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Adapts an existing EditorBlock-based canned block into the unified registry.
/// Legacy canned blocks remain atomic until named slots are explicitly defined.
/// </summary>
public sealed class LegacyPageEditorDefinitionAdapter :
    IPageEditorCatalogDefinition,
    INeoNodeFactory
{
    private static readonly EditorCapabilitySet DefaultCapabilities =
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Border |
        EditorCapabilitySet.Visibility |
        EditorCapabilitySet.Direction;

    private readonly IPageEditorBlockDefinition _definition;

    public LegacyPageEditorDefinitionAdapter(
        IPageEditorBlockDefinition definition,
        EditorCapabilitySet editorCapabilities = default)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        EditorCapabilities = editorCapabilities == default
            ? DefaultCapabilities
            : editorCapabilities;
    }

    public string CatalogId => _definition.CatalogId;
    public string DisplayName => _definition.DisplayName;
    public string? Description => _definition.Description;
    public string Category => _definition.Category;
    public NeoPageNodeKind Kind => ParseKind(_definition.Kind);
    public string IconName => _definition.IconName;
    public int SortOrder => _definition.SortOrder;
    public bool PublicStaticSsrSafe => _definition.PublicStaticSsrSafe;
    public Type? PreviewComponentType => _definition.PreviewComponentType;
    public Type? PropertyEditorComponentType => _definition.PropertyEditorComponentType;
    public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public EditorCapabilitySet EditorCapabilities { get; }

    public NeoPageNode CreateDefaultNode()
    {
        var node = _definition.ToNeoPageNode(_definition.CreateDefaultEditorBlock());
        node.CatalogId = CatalogId;
        node.Kind = Kind;
        return node;
    }

    public PageEditorDefinitionDescriptor ToDescriptor() =>
        new(this, this, LegacyDefinition: _definition);

    private static NeoPageNodeKind ParseKind(string? kind) =>
        Enum.TryParse<NeoPageNodeKind>(kind, true, out var parsed)
            ? parsed
            : NeoPageNodeKind.Block;
}
