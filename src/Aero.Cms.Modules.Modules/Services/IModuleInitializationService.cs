using Aero.Modular;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Service for initializing and persisting the state of modules during system setup.
/// </summary>
public interface IModuleInitializationService
{
    /// <summary>
    /// Projects an explicit descriptor catalog into built-in module state and persists it.
    /// </summary>
    /// <param name="descriptors">The source-generated module descriptors to persist.</param>
    /// <param name="ct">The token propagated to persistence.</param>
    /// <returns>A task that completes after the backing store commits the projected states.</returns>
    Task InitializeModulesAsync(IReadOnlyList<ModuleDescriptor> descriptors, CancellationToken ct = default);

    /// <summary>
    /// Legacy parameterless overload — no-op when no descriptors are supplied.
    /// Use <see cref="InitializeModulesAsync(IReadOnlyList{ModuleDescriptor}, CancellationToken)"/>
    /// to persist module state from a generated catalog.
    /// </summary>
    /// <param name="ct">A compatibility token; current implementations may ignore it.</param>
    /// <returns>A task representing the compatibility operation.</returns>
    [Obsolete("Use InitializeModulesAsync(IReadOnlyList<ModuleDescriptor>, CancellationToken) instead.")]
    Task InitializeModulesAsync(CancellationToken ct = default);
}
