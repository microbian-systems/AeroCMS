using Wolverine;

namespace Aero.Cms.Web.Generated;

/// <summary>
/// Stable Wolverine handler catalog API. Source generation populates handler registrations when available.
/// </summary>
public static partial class GeneratedWolverineHandlerCatalog
{
    public static void Register(WolverineOptions opts)
    {
        opts.Discovery.DisableConventionalDiscovery();
        RegisterGenerated(opts);
    }

    static partial void RegisterGenerated(WolverineOptions opts);
}
