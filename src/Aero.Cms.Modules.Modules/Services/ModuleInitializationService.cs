using Aero.Modular;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Default implementation of <see cref="IModuleInitializationService"/>.
/// </summary>
public sealed class ModuleInitializationService : IModuleInitializationService
{
    private readonly IModuleDiscoveryService _moduleDiscoveryService;
    private readonly IModuleStateStore _moduleStateStore;

    public ModuleInitializationService(
        IModuleDiscoveryService moduleDiscoveryService,
        IModuleStateStore moduleStateStore)
    {
        _moduleDiscoveryService = moduleDiscoveryService;
        _moduleStateStore = moduleStateStore;
    }

    /// <inheritdoc />
    public async Task InitializeModulesAsync(CancellationToken ct = default)
    {
        // Discover all modules available in the system
        var descriptors = await _moduleDiscoveryService.DiscoverAsync(ct);
        
        // Convert descriptors to state documents, marking them as built-in for the initial seed
        var moduleStates = descriptors.Select(d => ModuleDocument.FromDescriptor(d, isBuiltIn: true));
        
        // Persist the initial state to the database
        await _moduleStateStore.SaveAllAsync(moduleStates, ct);
    }
}
