using Aero.Actors;
using Aero.Actors.Abstractions;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Hosting.Defaults.Generated;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.Serialization;

namespace Aero.Cms.Hosting.Defaults;

/// <summary>
/// Provides the explicit production module set used by the standalone Aero CMS host.
/// Applications must opt in by passing <see cref="Catalog"/> to the hosting facade.
/// </summary>
public static class AeroCmsDefaultCatalog
{
    /// <summary>Gets the validated production catalog.</summary>
    public static AeroCmsHostCatalog Catalog { get; } = Create();

    private static AeroCmsHostCatalog Create()
    {
        var descriptors = GeneratedAeroModuleCatalog.Descriptors;
        var serverComponents = descriptors
            .Select(static descriptor => descriptor.ModuleType.Assembly)
            .Append(typeof(Aero.Cms.UI.App).Assembly)
            .DistinctBy(static assembly => assembly.FullName, StringComparer.Ordinal)
            .ToArray();

        var registration = new AeroCmsModuleRegistration(
            "aero-cms-defaults",
            descriptors,
            GeneratedWolverineHandlerCatalog.Register,
            ConfigureOrleans,
            serverComponents,
            [
                typeof(Aero.Cms.Web.Client._Imports).Assembly,
                typeof(Aero.Cms.Modules.Commerce.Client._Imports).Assembly
            ],
            [
                typeof(Aero.Cms.Modules.Aliases.Grains.AeroAliasGrain).Assembly,
                typeof(Aero.Cms.Modules.Content.Grains.AeroContentItemGrain).Assembly,
                typeof(Aero.Cms.Modules.Docs.Grains.AeroDocsGrain).Assembly,
                typeof(Aero.Cms.Modules.Media.Grains.AeroMediaGrain).Assembly,
                typeof(Aero.Cms.Modules.Pages.Grains.AeroPageGrain).Assembly,
                typeof(Aero.Cms.Modules.Posts.Grains.AeroPostGrain).Assembly,
                typeof(Aero.Cms.Modules.Settings.Grains.AeroSettingGrain).Assembly
            ],
            AeroCmsCapabilities.ServerComponents |
            AeroCmsCapabilities.WebAssemblyComponents |
            AeroCmsCapabilities.Identity |
            AeroCmsCapabilities.Setup |
            AeroCmsCapabilities.PublicQuery |
            AeroCmsCapabilities.Manager);

        return new AeroCmsHostCatalogBuilder().Add(registration).Build();
    }

    private static void ConfigureOrleans(ISiloBuilder silo)
    {
        silo.Services.AddSerializer(serializer =>
        {
            serializer.AddAssembly(typeof(IPongGrain).Assembly);
            serializer.AddAssembly(typeof(Message).Assembly);
            serializer.AddAssembly(typeof(IAeroPageActor).Assembly);
        });
    }
}
