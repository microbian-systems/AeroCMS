using Aero.Actors;
using Aero.Actors.Abstractions;
using Aero.Cms.Abstractions.Actors;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;

namespace Aero.Cms.Web.Generated;

/// <summary>
/// Grain assembly catalog — registers grain assemblies with the Orleans silo.
/// Mirrors the <c>GeneratedWolverineHandlerCatalog.Register</c> callback pattern.
///
/// Grain assemblies are auto-discovered by Orleans from loaded assemblies when
/// the module system registers module types in DI before the silo starts.
/// Explicit application part registration is added per-module as needed.
///
/// SOURCE GENERATOR TARGET: This file should be replaced by a source generator
/// that scans for <c>AeroActor</c> subclasses and emits the <c>Register</c>
/// method automatically.
/// </summary>
public static class GeneratedAeroGrainCatalog
{
    /// <summary>
    /// Configures the Orleans silo builder for grain assembly discovery.
    /// Called by the <c>configureGrains</c> callback in
    /// <c>Program.cs → AddAeroApplicationServer</c>.
    /// </summary>
    public static void Register(ISiloBuilder silo)
    {
        silo.Services.AddSerializer(serializer =>
        {
            serializer.AddAssembly(typeof(IPongGrain).Assembly);
            serializer.AddAssembly(typeof(Message).Assembly);
            serializer.AddAssembly(typeof(IAeroPageActor).Assembly);
        });

        // Assemblies containing grains (13 grains across 8 modules):
        //   Aero.Cms.Modules.Aliases     — AeroAliasGrain
        //   Aero.Cms.Modules.Posts       — AeroCategoryGrain, AeroSeriesGrain, AeroTagGrain, AeroPostGrain
        //   Aero.Cms.Modules.Content     — AeroContentItemGrain, AeroContentTypeGrain
        //   Aero.Cms.Modules.Docs        — AeroDocsGrain
        //   Aero.Cms.Modules.Media       — AeroMediaGrain
        //   Aero.Cms.Modules.Pages       — AeroPageGrain
        //   Aero.Cms.Modules.Settings    — AeroSettingGrain
        //
        // Excluded by design: Identity/Users (EF Core, not Marten)
        // Skipped: AeroThemeGrain — Theme module has no persistence logic to port;
        //          API endpoints are TODO stubs, ThemeService enumerates loaded modules.
        //
        // Orleans auto-discovers grains from loaded assemblies.
    }
}
