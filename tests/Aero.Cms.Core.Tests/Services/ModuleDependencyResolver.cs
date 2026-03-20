using Aero.Cms.Core.Modules;

namespace Aero.Cms.Core.Tests.Services;

/// <summary>
/// Exception thrown when dependency resolution fails.
/// </summary>
public class DependencyResolutionException : Exception
{
    public string? OffendingModule { get; }
    public IReadOnlyList<string>? MissingDependencies { get; }
    public IReadOnlyList<string>? CycleMembers { get; }

    public DependencyResolutionException(string message) : base(message) { }

    public DependencyResolutionException(string message, string offendingModule, IReadOnlyList<string> missingDependencies)
        : base(message)
    {
        OffendingModule = offendingModule;
        MissingDependencies = missingDependencies;
    }

    public DependencyResolutionException(string message, IReadOnlyList<string> cycleMembers)
        : base(message)
    {
        CycleMembers = cycleMembers;
    }
}

/// <summary>
/// Service responsible for resolving module dependencies and calculating load order.
/// </summary>
public interface IModuleDependencyResolver
{
    /// <summary>
    /// Resolves dependencies and returns a ModuleGraph with the correct load order.
    /// </summary>
    /// <exception cref="DependencyResolutionException">Thrown when dependencies are missing or circular.</exception>
    Task<ModuleGraph> ResolveAsync(IEnumerable<ModuleDescriptor> modules, CancellationToken ct = default);
}

/// <summary>
/// Default implementation using Kahn's algorithm for topological sorting.
/// </summary>
public class ModuleDependencyResolver : IModuleDependencyResolver
{
    public Task<ModuleGraph> ResolveAsync(IEnumerable<ModuleDescriptor> modules, CancellationToken ct = default)
    {
        var moduleList = modules.ToList();
        var moduleDict = moduleList.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        // Validate all dependencies exist
        ValidateDependenciesExist(moduleList, moduleDict);

        // Check for circular dependencies
        CheckForCycles(moduleList, moduleDict);

        // Build dependency graph and compute load order using Kahn's algorithm
        var loadOrder = ComputeLoadOrder(moduleList, moduleDict);

        var graph = new ModuleGraph
        {
            Modules = moduleDict,
            LoadOrder = loadOrder
        };

        return Task.FromResult(graph);
    }

    private static void ValidateDependenciesExist(List<ModuleDescriptor> modules, Dictionary<string, ModuleDescriptor> moduleDict)
    {
        foreach (var module in modules)
        {
            var missingDeps = module.Dependencies
                .Where(dep => !moduleDict.ContainsKey(dep))
                .ToList();

            if (missingDeps.Count > 0)
            {
                throw new DependencyResolutionException(
                    $"Module '{module.Name}' has missing dependencies: {string.Join(", ", missingDeps)}",
                    module.Name,
                    missingDeps);
            }
        }
    }

    private static void CheckForCycles(List<ModuleDescriptor> modules, Dictionary<string, ModuleDescriptor> moduleDict)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var module in modules)
        {
            if (DetectCycle(module.Name, moduleDict, visited, recursionStack, new List<string>()))
            {
                return; // Exception already thrown
            }
        }
    }

    private static bool DetectCycle(
        string moduleName,
        Dictionary<string, ModuleDescriptor> moduleDict,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path)
    {
        if (recursionStack.Contains(moduleName))
        {
            // Found a cycle - build the cycle path
            var cycleStart = path.IndexOf(moduleName);
            var cycle = path.Skip(cycleStart).Concat(new[] { moduleName }).ToList();
            throw new DependencyResolutionException(
                $"Circular dependency detected: {string.Join(" -> ", cycle)}",
                cycle);
        }

        if (visited.Contains(moduleName))
        {
            return false;
        }

        visited.Add(moduleName);
        recursionStack.Add(moduleName);
        path.Add(moduleName);

        if (moduleDict.TryGetValue(moduleName, out var module))
        {
            foreach (var dep in module.Dependencies)
            {
                if (DetectCycle(dep, moduleDict, visited, recursionStack, path))
                {
                    return true;
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(moduleName);
        return false;
    }

    private static IReadOnlyList<ModuleDescriptor> ComputeLoadOrder(
        List<ModuleDescriptor> modules,
        Dictionary<string, ModuleDescriptor> moduleDict)
    {
        // Build adjacency list (dependency -> dependent modules)
        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            inDegree[module.Name] = 0;
            dependents[module.Name] = new List<string>();
        }

        foreach (var module in modules)
        {
            foreach (var dep in module.Dependencies)
            {
                if (dependents.TryGetValue(dep, out var list))
                {
                    list.Add(module.Name);
                    inDegree[module.Name]++;
                }
            }
        }

        // Kahn's algorithm with stable ordering
        var queue = new SortedSet<(int Order, string Name)>();
        foreach (var kvp in inDegree.Where(k => k.Value == 0))
        {
            var module = moduleDict[kvp.Key];
            queue.Add((module.Order, kvp.Key));
        }

        var result = new List<ModuleDescriptor>();

        while (queue.Count > 0)
        {
            var (_, name) = queue.Min;
            queue.Remove(queue.Min);

            result.Add(moduleDict[name]);

            if (!dependents.TryGetValue(name, out var dependentList))
                continue;

            foreach (var dependent in dependentList)
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    var module = moduleDict[dependent];
                    queue.Add((module.Order, dependent));
                }
            }
        }

        return result;
    }
}
