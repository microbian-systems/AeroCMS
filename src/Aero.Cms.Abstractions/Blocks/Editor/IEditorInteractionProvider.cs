namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Exposes the <see cref="EditorInteractionCapabilities"/> for a node
/// definition. Separate from <see cref="IPageEditorCatalogDefinition"/> to
/// follow Interface Segregation — not every catalog participant needs
/// interaction capabilities, and legacy adapters may wrap it independently.
///
/// Final architecture. <see cref="PageEditorCatalogDefinitionBase"/>
/// implements this with an abstract <c>Interaction</c> property. The
/// <c>IEditorNodeActionProvider</c> service consumes it at runtime.
/// </summary>
public interface IEditorInteractionProvider
{
        /// <summary>
    /// Gets or sets the Interaction.
    /// </summary>
EditorInteractionCapabilities Interaction { get; }
}
