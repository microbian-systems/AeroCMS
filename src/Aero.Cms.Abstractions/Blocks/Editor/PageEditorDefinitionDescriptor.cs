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
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => Catalog.CatalogId;

    /// <summary>
    /// Canvas interaction capabilities for this definition.
    /// Derived from <see cref="IEditorInteractionProvider"/> on the
    /// <see cref="Catalog"/> definition when present. Otherwise applies the
    /// default page-editor interaction contract expected by visual composition
    /// nodes: selectable, editable, draggable, duplicatable, deletable, and
    /// copyable; containers additionally become paste targets.
    /// </summary>
    public EditorInteractionCapabilities Interaction =>
        (Catalog as IEditorInteractionProvider)?.Interaction
        ?? DefaultInteraction(Catalog);

    private static EditorInteractionCapabilities DefaultInteraction(
        IPageEditorCatalogDefinition catalog)
    {
        var interaction =
            EditorInteractionCapabilities.Selectable |
            EditorInteractionCapabilities.Editable |
            EditorInteractionCapabilities.Draggable |
            EditorInteractionCapabilities.Duplicatable |
            EditorInteractionCapabilities.Deletable |
            EditorInteractionCapabilities.Copyable;

        if (catalog.Composition.CanContainChildren)
        {
            interaction |= EditorInteractionCapabilities.PasteTarget;
        }

        return interaction;
    }
}
