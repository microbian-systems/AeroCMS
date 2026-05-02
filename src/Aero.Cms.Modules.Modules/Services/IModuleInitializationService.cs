using Aero.Modular;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Service for initializing and persisting the state of modules during system setup.
/// </summary>
public interface IModuleInitializationService
{
    /// <summary>
    /// Discovers all available modules and persists their initial state to the database.
    /// When <paramref name="descriptors"/> is provided, uses them directly instead of
    /// calling legacy reflection-based discovery.
    /// </summary>
    /// <param name="descriptors">Source-generated module descriptors. Pass non-null to skip discovery.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InitializeModulesAsync(IReadOnlyList<ModuleDescriptor> descriptors, CancellationToken ct = default);

    /// <summary>
    /// Legacy parameterless overload — no-op when no descriptors are supplied.
    /// Use <see cref="InitializeModulesAsync(IReadOnlyList{ModuleDescriptor}, CancellationToken)"/>
    /// to persist module state from a generated catalog.
    /// </summary>
    [Obsolete("Use InitializeModulesAsync(IReadOnlyList<ModuleDescriptor>, CancellationToken) instead.")]
    Task InitializeModulesAsync(CancellationToken ct = default);
}
