using Aero.Modular;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Modules.Services;

/// <summary>
/// Default implementation of <see cref="IModuleRuntimeStateMerger"/>.
/// Merges discovered module descriptors with stored state from <see cref="IModuleStateStore"/>.
/// </summary>
public sealed class ModuleRuntimeStateMerger(
    IModuleStateStore? stateStore,
    ILogger<ModuleRuntimeStateMerger> logger)
    : IModuleRuntimeStateMerger
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleDescriptor>> MergeAsync(
        IReadOnlyList<ModuleDescriptor> discovered,
        CancellationToken ct = default)
    {
        if (stateStore is null)
        {
            logger.LogDebug("No IModuleStateStore available — skipping stored state merge.");
            return discovered;
        }

        var storedStates = await stateStore.GetAllAsync(ct);

        if (storedStates.Count == 0)
        {
            logger.LogDebug("No stored module states found — using discovered descriptors as-is.");
            return discovered;
        }

        var storedByName = storedStates.ToDictionary(
            s => s.Name,
            s => s,
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<ModuleDescriptor>(discovered.Count);
        foreach (var descriptor in discovered)
        {
            if (storedByName.TryGetValue(descriptor.Name, out var stored))
            {
                merged.Add(Merge(descriptor, stored));
                logger.LogDebug("Merged stored state for module '{ModuleName}'.", descriptor.Name);
            }
            else
            {
                merged.Add(descriptor);
                logger.LogDebug("Module '{ModuleName}' not found in stored state — using discovered data.", descriptor.Name);
            }
        }

        logger.LogInformation(
            "Merged {Count} module descriptor(s) with stored state.",
            merged.Count);

        return merged;
    }

    private static ModuleDescriptor Merge(ModuleDescriptor discovered, ModuleDocument stored)
    {
        return new ModuleDescriptor
        {
            Name = discovered.Name,
            Version = discovered.Version,
            Author = discovered.Author,
            ModuleType = discovered.ModuleType,
            Dependencies = stored.Dependencies.Count > 0 ? stored.Dependencies : discovered.Dependencies,
            AssemblyName = discovered.AssemblyName,
            PhysicalPath = discovered.PhysicalPath,
            IsUiModule = discovered.IsUiModule,
            IsApiModule = discovered.IsApiModule,
            IsBackgroundModule = discovered.IsBackgroundModule,
            IsThemeModule = discovered.IsThemeModule,
            IsAdminModule = discovered.IsAdminModule,
            IsFilterModule = discovered.IsFilterModule,
            IsContentDefinitionModule = discovered.IsContentDefinitionModule,
            IsMartenConfigurator = discovered.IsMartenConfigurator,
            IsAsyncMartenConfigurator = discovered.IsAsyncMartenConfigurator,
            Order = stored.Order,
            Category = stored.Category.Count > 0 ? stored.Category : discovered.Category,
            Tags = stored.Tags.Count > 0 ? stored.Tags : discovered.Tags,
            DisabledInProduction = stored.DisabledInProduction,
            Disabled = stored.Disabled,
            Description = discovered.Description
        };
    }
}
