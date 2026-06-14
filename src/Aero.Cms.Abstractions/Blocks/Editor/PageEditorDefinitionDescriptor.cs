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
}
