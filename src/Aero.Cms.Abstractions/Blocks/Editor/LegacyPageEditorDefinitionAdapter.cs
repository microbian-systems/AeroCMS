using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Adapts an existing EditorBlock-based canned block into the unified registry.
/// Legacy canned blocks remain atomic until named slots are explicitly defined.
/// </summary>
public sealed class LegacyPageEditorDefinitionAdapter :
    IPageEditorCatalogDefinition,
    INeoNodeFactory,
    IEditorInteractionProvider
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

        /// <summary>
    /// Initializes a new instance of the <see cref="LegacyPageEditorDefinitionAdapter"/> class.
    /// </summary>
public LegacyPageEditorDefinitionAdapter(
        IPageEditorBlockDefinition definition,
        EditorCapabilitySet editorCapabilities = default)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        EditorCapabilities = editorCapabilities == default
            ? DefaultCapabilities
            : editorCapabilities;
    }

        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => _definition.CatalogId;
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => _definition.DisplayName;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => _definition.Description;
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => _definition.Category;
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public NeoPageNodeKind Kind => ParseKind(_definition.Kind);
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => _definition.IconName;
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => _definition.SortOrder;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => _definition.PublicStaticSsrSafe;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => _definition.PreviewComponentType;
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => _definition.PropertyEditorComponentType;
        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
public ICompositionCapabilities Composition { get; } =
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public EditorCapabilitySet EditorCapabilities { get; }

    /// <summary>
    /// Legacy canned blocks support all canvas interaction capabilities
    /// (select, edit, drag, duplicate, delete, copy).
    /// </summary>
    public EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable;

        /// <summary>
    /// CreateDefaultNode method.
    /// </summary>
public NeoPageNode CreateDefaultNode()
    {
        var node = _definition.ToNeoPageNode(_definition.CreateDefaultEditorBlock());
        node.CatalogId = CatalogId;
        node.Kind = Kind;
        return node;
    }

        /// <summary>
    /// ToDescriptor method.
    /// </summary>
public PageEditorDefinitionDescriptor ToDescriptor() =>
        new(this, this, LegacyDefinition: _definition);

    private static NeoPageNodeKind ParseKind(string? kind) =>
        Enum.TryParse<NeoPageNodeKind>(kind, true, out var parsed)
            ? parsed
            : NeoPageNodeKind.Block;
}
