namespace Aero.Cms.Core.Modules;

/// <summary>
/// Exception thrown when a module system error occurs during discovery, validation, or loading.
/// </summary>
public abstract class ModuleSystemException : Exception
{
    protected ModuleSystemException(string message) : base(message) { }
    protected ModuleSystemException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when a duplicate module name is detected during discovery.
/// </summary>
public sealed class DuplicateModuleNameException : ModuleSystemException
{
    public string ModuleName { get; }
    public string FirstAssembly { get; }
    public string SecondAssembly { get; }

    public DuplicateModuleNameException(string moduleName, string firstAssembly, string secondAssembly)
        : base($"Duplicate module name '{moduleName}' detected. First defined in '{firstAssembly}', then in '{secondAssembly}'.")
    {
        ModuleName = moduleName;
        FirstAssembly = firstAssembly;
        SecondAssembly = secondAssembly;
    }
}

/// <summary>
/// Exception thrown when a module has a dependency that cannot be found.
/// </summary>
public sealed class MissingModuleDependencyException : ModuleSystemException
{
    public string ModuleName { get; }
    public string MissingDependency { get; }
    public IReadOnlyList<string> AvailableModules { get; }

    public MissingModuleDependencyException(string moduleName, string missingDependency, IEnumerable<string> availableModules)
        : base($"Module '{moduleName}' depends on '{missingDependency}' which was not found. Available modules: {string.Join(", ", availableModules)}.")
    {
        ModuleName = moduleName;
        MissingDependency = missingDependency;
        AvailableModules = availableModules.ToList().AsReadOnly();
    }
}

/// <summary>
/// Exception thrown when a circular dependency is detected in the module graph.
/// </summary>
public sealed class CircularDependencyException : ModuleSystemException
{
    public IReadOnlyList<string> CyclePath { get; }

    public CircularDependencyException(IEnumerable<string> cyclePath)
        : base($"Circular dependency detected: {string.Join(" -> ", cyclePath)} -> {cyclePath.FirstOrDefault()}.")
    {
        CyclePath = cyclePath.ToList().AsReadOnly();
    }
}

/// <summary>
/// Exception thrown when an assembly fails to load during module discovery.
/// </summary>
public sealed class ModuleAssemblyLoadException : ModuleSystemException
{
    public string AssemblyName { get; }
    public string? AssemblyPath { get; }

    public ModuleAssemblyLoadException(string assemblyName, string? assemblyPath, Exception inner)
        : base($"Failed to load assembly '{assemblyName}'{(assemblyPath != null ? $" from '{assemblyPath}'" : "")}.", inner)
    {
        AssemblyName = assemblyName;
        AssemblyPath = assemblyPath;
    }
}

/// <summary>
/// Exception thrown when a tenant enables a module that was not discovered.
/// </summary>
public sealed class UnknownModuleException : ModuleSystemException
{
    public string ModuleName { get; }
    public string? TenantId { get; }

    public UnknownModuleException(string moduleName, string? tenantId = null)
        : base(tenantId != null
            ? $"Tenant '{tenantId}' enabled unknown module '{moduleName}'."
            : $"Unknown module '{moduleName}'.")
    {
        ModuleName = moduleName;
        TenantId = tenantId;
    }
}
