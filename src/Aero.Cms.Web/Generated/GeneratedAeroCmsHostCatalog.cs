using Aero.Cms.Web.Bootstrap;

namespace Aero.Cms.Web.Generated;

/// <summary>
/// Connects the host's source-generated module, Wolverine, and Orleans catalogs to Aero CMS.
/// </summary>
internal static class GeneratedAeroCmsHostCatalog
{
    /// <summary>Applies all generated host catalogs to the Aero CMS registration options.</summary>
    internal static void Configure(AeroCmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ModuleDescriptors = GeneratedAeroModuleCatalog.Descriptors;
        options.ConfigureWolverine = GeneratedWolverineHandlerCatalog.Register;
        options.ConfigureGrains = GeneratedAeroGrainCatalog.Register;
    }
}
