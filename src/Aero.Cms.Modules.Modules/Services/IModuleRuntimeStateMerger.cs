using Aero.Modular;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Merges source-generated (or reflection-discovered) module descriptors
/// with stored module state from the database.
/// </summary>
/// <remarks>
/// Stored state overrides take precedence for properties that users can
/// configure at runtime: <c>Order</c>, <c>Category</c>, <c>Tags</c>,
/// <c>Disabled</c>, <c>DisabledInProduction</c>, and <c>Dependencies</c>.
/// Empty stored category, tag, and dependency collections may be treated as
/// absence and fall back to discovered metadata.
/// This service decouples state merging from discovery, allowing both
/// source-generated and reflection-based descriptors to flow through the
/// same merge pipeline.
/// </remarks>
public interface IModuleRuntimeStateMerger
{
    /// <summary>
    /// Merges the discovered descriptors with stored module state.
    /// </summary>
    /// <param name="discovered">The descriptors from source generation or reflection.</param>
    /// <param name="ct">The token propagated to stored-state retrieval.</param>
    /// <returns>
    /// Merged descriptors with stored state applied, or the discovered list when no state store
    /// or stored rows are available.
    /// </returns>
    Task<IReadOnlyList<ModuleDescriptor>> MergeAsync(
        IReadOnlyList<ModuleDescriptor> discovered,
        CancellationToken ct = default);
}
