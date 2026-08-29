using System.Reflection;
using Aero.Modular;

namespace Aero.Cms.Hosting;

/// <summary>
/// Composes and validates explicitly selected Aero CMS package registrations.
/// </summary>
public sealed class AeroCmsHostCatalogBuilder
{
    private readonly List<AeroCmsModuleRegistration> _registrations = [];

    /// <summary>Adds one explicit package registration.</summary>
    /// <param name="registration">Registration to add.</param>
    /// <returns>The same builder.</returns>
    public AeroCmsHostCatalogBuilder Add(AeroCmsModuleRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations.Add(registration);
        return this;
    }

    /// <summary>Builds a deterministic, immutable catalog.</summary>
    /// <returns>The validated host catalog.</returns>
    /// <exception cref="AeroCmsCatalogException">The selected catalog is empty or invalid.</exception>
    public AeroCmsHostCatalog Build()
    {
        if (_registrations.Count == 0)
        {
            throw Failure(
                "AEROCMS_CATALOG_EMPTY",
                "At least one Aero CMS module registration must be selected.");
        }

        var registrations = _registrations
            .OrderBy(static registration => registration.Id, StringComparer.Ordinal)
            .ToArray();

        var duplicateRegistration = registrations
            .GroupBy(static registration => registration.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateRegistration is not null)
        {
            throw Failure(
                "AEROCMS_CATALOG_DUPLICATE_REGISTRATION",
                $"Aero CMS registration id '{duplicateRegistration.Key}' was selected more than once.");
        }

        var descriptors = registrations
            .SelectMany(static registration => registration.ModuleDescriptors)
            .ToArray();
        if (descriptors.Length == 0)
        {
            throw Failure(
                "AEROCMS_CATALOG_NO_MODULES",
                "The selected Aero CMS registrations did not provide any module descriptors.");
        }

        ValidateDescriptors(descriptors);
        registrations = TopologicalSortRegistrations(registrations);
        var orderedDescriptors = TopologicalSort(descriptors);
        var serverAssemblies = DistinctAssemblies(
            registrations.SelectMany(static registration => registration.ServerComponentAssemblies));
        var webAssemblyAssemblies = DistinctAssemblies(
            registrations.SelectMany(static registration => registration.WebAssemblyComponentAssemblies));
        var capabilities = registrations.Aggregate(
            AeroCmsCapabilities.None,
            static (current, registration) => current | registration.Capabilities);

        return new AeroCmsHostCatalog(
            registrations,
            orderedDescriptors,
            serverAssemblies,
            webAssemblyAssemblies,
            capabilities);
    }

    private static AeroCmsModuleRegistration[] TopologicalSortRegistrations(
        IReadOnlyList<AeroCmsModuleRegistration> registrations)
    {
        var ownerByModule = registrations
            .SelectMany(registration => registration.ModuleDescriptors.Select(
                descriptor => (descriptor.Name, registration.Id)))
            .ToDictionary(static item => item.Name, static item => item.Id, StringComparer.OrdinalIgnoreCase);
        var byId = registrations.ToDictionary(
            static registration => registration.Id,
            StringComparer.OrdinalIgnoreCase);
        var dependencies = registrations.ToDictionary(
            static registration => registration.Id,
            registration => registration.ModuleDescriptors
                .SelectMany(static descriptor => descriptor.Dependencies)
                .Select(dependency => ownerByModule[dependency])
                .Where(owner => !string.Equals(owner, registration.Id, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var dependents = registrations.ToDictionary(
            static registration => registration.Id,
            static _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (registration, required) in dependencies)
        {
            foreach (var dependency in required)
            {
                dependents[dependency].Add(registration);
            }
        }

        var ready = new SortedSet<string>(
            dependencies.Where(static pair => pair.Value.Count == 0).Select(static pair => pair.Key),
            StringComparer.Ordinal);
        var ordered = new List<AeroCmsModuleRegistration>(registrations.Count);
        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            ordered.Add(byId[id]);

            foreach (var dependent in dependents[id].Order(StringComparer.Ordinal))
            {
                dependencies[dependent].Remove(id);
                if (dependencies[dependent].Count == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (ordered.Count != registrations.Count)
        {
            throw Failure(
                "AEROCMS_CATALOG_REGISTRATION_DEPENDENCY_CYCLE",
                "The selected Aero CMS registrations contain a dependency cycle.");
        }

        return ordered.ToArray();
    }

    private static void ValidateDescriptors(IReadOnlyList<ModuleDescriptor> descriptors)
    {
        var duplicateModule = descriptors
            .GroupBy(static descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateModule is not null)
        {
            throw Failure(
                "AEROCMS_CATALOG_DUPLICATE_MODULE",
                $"Module name '{duplicateModule.Key}' is provided more than once.");
        }

        var names = descriptors
            .Select(static descriptor => descriptor.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Name))
            {
                throw Failure(
                    "AEROCMS_CATALOG_INVALID_MODULE",
                    "A selected module descriptor has an empty name.");
            }

            if (descriptor.ModuleType is null ||
                !typeof(IAeroModule).IsAssignableFrom(descriptor.ModuleType) ||
                descriptor.ModuleType.IsAbstract)
            {
                throw Failure(
                    "AEROCMS_CATALOG_INVALID_MODULE_TYPE",
                    $"Module '{descriptor.Name}' must identify a concrete {nameof(IAeroModule)} implementation.");
            }

            if (string.IsNullOrWhiteSpace(descriptor.AssemblyName))
            {
                throw Failure(
                    "AEROCMS_CATALOG_INVALID_MODULE_ASSEMBLY",
                    $"Module '{descriptor.Name}' has no assembly identity.");
            }

            foreach (var dependency in descriptor.Dependencies)
            {
                if (!names.Contains(dependency))
                {
                    throw Failure(
                        "AEROCMS_CATALOG_MISSING_DEPENDENCY",
                        $"Module '{descriptor.Name}' depends on missing module '{dependency}'.");
                }
            }
        }
    }

    private static IReadOnlyList<ModuleDescriptor> TopologicalSort(
        IReadOnlyList<ModuleDescriptor> descriptors)
    {
        var byName = descriptors.ToDictionary(
            static descriptor => descriptor.Name,
            StringComparer.OrdinalIgnoreCase);
        var dependents = descriptors.ToDictionary(
            static descriptor => descriptor.Name,
            static _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        var indegrees = descriptors.ToDictionary(
            static descriptor => descriptor.Name,
            static descriptor => descriptor.Dependencies.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors)
        {
            foreach (var dependency in descriptor.Dependencies)
            {
                dependents[dependency].Add(descriptor.Name);
            }
        }

        var ready = new SortedSet<string>(
            indegrees.Where(static item => item.Value == 0).Select(static item => item.Key),
            StringComparer.Ordinal);
        var ordered = new List<ModuleDescriptor>(descriptors.Count);

        while (ready.Count > 0)
        {
            var name = ready.Min!;
            ready.Remove(name);
            ordered.Add(byName[name]);

            foreach (var dependent in dependents[name].Order(StringComparer.Ordinal))
            {
                indegrees[dependent]--;
                if (indegrees[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (ordered.Count != descriptors.Count)
        {
            var cycleMembers = indegrees
                .Where(static item => item.Value > 0)
                .Select(static item => item.Key)
                .Order(StringComparer.Ordinal);
            throw Failure(
                "AEROCMS_CATALOG_DEPENDENCY_CYCLE",
                $"The selected Aero CMS modules contain a dependency cycle: {string.Join(", ", cycleMembers)}.");
        }

        return ordered;
    }

    private static IReadOnlyList<Assembly> DistinctAssemblies(IEnumerable<Assembly> assemblies)
        => assemblies
            .GroupBy(
                static assembly => assembly.FullName ?? assembly.GetName().Name ?? string.Empty,
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(
                static assembly => assembly.FullName ?? assembly.GetName().Name,
                StringComparer.Ordinal)
            .ToArray();

    private static AeroCmsCatalogException Failure(string code, string message)
        => new(code, message);
}
