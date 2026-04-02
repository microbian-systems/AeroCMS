using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Aero.AppServer;

public static class ScannerExtensions
{
    public static ISiloBuilder AddApplicationGrains(this ISiloBuilder siloBuilder)
    {
        //siloBuilder.ConfigureApplicationParts(parts =>
        //{
        //    var entryAssembly = Assembly.GetEntryAssembly();
        //    if (entryAssembly == null) return;

        //    // Only assemblies explicitly referenced by the entry point
        //    var trustedAssemblies = new[] { entryAssembly }
        //        .Concat(entryAssembly.GetReferencedAssemblies()
        //            .Select(Assembly.Load))
        //        .Where(a => !a.IsDynamic)
        //        .Distinct();

        //    foreach (var assembly in trustedAssemblies)
        //    {
        //        if (HasGrainImplementations(assembly))
        //        {
        //            parts.AddApplicationPart(assembly);
        //        }
        //    }
        //});

        return siloBuilder;
    }

    private static bool HasGrainImplementations(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Any(t => t.IsClass &&
                          !t.IsAbstract &&
                          typeof(IGrain).IsAssignableFrom(t));
        }
        catch (ReflectionTypeLoadException)
        {
            return false;
        }
    }
}