namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Read-only lookup service for all page-editor definitions installed in the
/// application. Implementations are built from DI-registered providers and
/// should be immutable after construction.
///
/// Pattern: Registry plus Strategy. Providers supply definitions; consumers
/// query this abstraction instead of depending on concrete provider classes or
/// static mutable state.
/// </summary>
public interface IPageEditorDefinitionRegistry
{
    /// <summary>
    /// Attempts to resolve a catalog descriptor by its stable catalog ID.
    /// </summary>
    bool TryGetDescriptor(
        string? catalogId,
        out PageEditorDefinitionDescriptor descriptor);

    /// <summary>
    /// All registered descriptors, including legacy adapters and native node
    /// definitions.
    /// </summary>
    IReadOnlyCollection<PageEditorDefinitionDescriptor> AllDescriptors { get; }

    /// <summary>
    /// Transitional view over descriptors that still wrap legacy
    /// <see cref="IPageEditorBlockDefinition"/> instances.
    /// </summary>
    IReadOnlyCollection<IPageEditorBlockDefinition> LegacyDefinitions { get; }
}
