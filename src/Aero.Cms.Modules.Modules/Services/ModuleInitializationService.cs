using Aero.Modular;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Default implementation of <see cref="IModuleInitializationService"/>.
/// Module state is persisted from an explicit descriptor list, eliminating
/// the need for runtime reflection-based assembly scanning.
/// </summary>
public sealed class ModuleInitializationService : IModuleInitializationService
{
    private readonly IModuleStateStore _moduleStateStore;
        
        /// <summary>
    /// Initializes the service with the module-state persistence boundary.
    /// </summary>
    /// <param name="moduleStateStore">The store used to persist generated descriptor state.</param>
public ModuleInitializationService(
        IModuleStateStore moduleStateStore)
    {
        _moduleStateStore = moduleStateStore;
    }

    /// <summary>
    /// Persists module state from the provided descriptors.
    /// This is the main path — callers supply descriptors from a
    /// source-generated catalog.
    /// </summary>
    /// <param name="descriptors">The descriptors projected into built-in module documents.</param>
    /// <param name="ct">The token used by the single store save operation.</param>
    /// <remarks>Existing documents with matching identities may be overwritten by the backing store.</remarks>
    public async Task InitializeModulesAsync(IReadOnlyList<ModuleDescriptor> descriptors, CancellationToken ct = default)
    {
        var moduleStates = descriptors.Select(d => ModuleDocument.FromDescriptor(d, isBuiltIn: true));
        await _moduleStateStore.SaveAllAsync(moduleStates, ct);
    }

    /// <summary>
    /// Legacy parameterless overload — kept for interface compatibility.
    /// No-op: does not perform reflection discovery. Callers must supply
    /// descriptors via <see cref="InitializeModulesAsync(IReadOnlyList{ModuleDescriptor}, CancellationToken)"/>.
    /// </summary>
    /// <param name="ct">Ignored because this compatibility overload performs no work.</param>
    /// <returns>A task already completed without accessing the state store.</returns>
    [Obsolete("Use InitializeModulesAsync(IReadOnlyList<ModuleDescriptor>, CancellationToken) instead.")]
    public Task InitializeModulesAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
