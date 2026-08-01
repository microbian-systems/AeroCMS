using Aero.Cms.Modules.Commerce.Client.Routing;
using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Cms.Shared.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Commerce.Client;

/// <summary>Registers Commerce browser clients and explicit manager routes.</summary>
public static class CommerceClientServiceCollectionExtensions
{
    /// <summary>Adds customer and manager Commerce clients plus the module route assembly.</summary>
    public static IServiceCollection AddAeroCommerceClient(this IServiceCollection services)
    {
        services.AddSingleton<IManagerRouteAssemblyProvider, CommerceManagerRouteAssemblyProvider>();
        services.AddHttpClient<ICommerceClientService, CommerceClientService>();
        services.AddHttpClient<ICommerceManagerClient, CommerceManagerHttpClient>();
        return services;
    }
}
