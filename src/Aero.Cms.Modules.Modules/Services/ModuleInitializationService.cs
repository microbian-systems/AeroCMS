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
    /// Initializes a new instance of the <see cref="ModuleInitializationService"/> class.
    /// </summary>
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
    [Obsolete("Use InitializeModulesAsync(IReadOnlyList<ModuleDescriptor>, CancellationToken) instead.")]
    public Task InitializeModulesAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
