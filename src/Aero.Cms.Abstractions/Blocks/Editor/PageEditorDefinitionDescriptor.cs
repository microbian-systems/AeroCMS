namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Immutable registry entry composed from purpose-specific editor contracts.
/// </summary>
public sealed record PageEditorDefinitionDescriptor(
    IPageEditorCatalogDefinition Catalog,
    INeoNodeFactory NodeFactory,
    INeoNodeBlockMapper? BlockMapper = null,
    IPageEditorBlockDefinition? LegacyDefinition = null)
{
    public string CatalogId => Catalog.CatalogId;

    /// <summary>
    /// Canvas interaction capabilities for this definition.
    /// Derived from <see cref="IEditorInteractionProvider"/> on the
    /// <see cref="Catalog"/> definition. Returns <see cref="EditorInteractionCapabilities.None"/>
    /// when the catalog definition does not implement the interaction contract.
    /// </summary>
    public EditorInteractionCapabilities Interaction =>
        (Catalog as IEditorInteractionProvider)?.Interaction
        ?? EditorInteractionCapabilities.None;
}
