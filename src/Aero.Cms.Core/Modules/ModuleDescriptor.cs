namespace Aero.Cms.Core.Modules;

/// <summary>
/// Normalized metadata for a discovered module.
/// </summary>
public sealed class ModuleDescriptor
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }
    public required Type ModuleType { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public required string AssemblyName { get; init; }
    public string? PhysicalPath { get; init; }
    public bool IsUiModule { get; init; }
}
