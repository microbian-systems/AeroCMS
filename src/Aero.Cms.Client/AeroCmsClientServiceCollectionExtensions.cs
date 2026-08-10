using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Contracts.Abstractions;
using Aero.Cms.Contracts.Services;
using Aero.Cms.Modules.Commerce.Client;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Client.Services;
using Aero.Core.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeoUI.Blazor;
using NeoUI.Blazor.Extensions;
using NeoUI.Blazor.Primitives.Extensions;
using Radzen;

namespace Aero.Cms.Web.Client;

/// <summary>Registers reusable Aero CMS browser services in a thin WebAssembly companion.</summary>
public static class AeroCmsClientServiceCollectionExtensions
{
    /// <summary>Adds browser-side Aero CMS services for the consuming host origin.</summary>
    public static IServiceCollection AddAeroCmsClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string hostBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostBaseAddress);

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        services.AddScoped<ServerAuthenticationStateProvider>();
        services.AddSingleton<IFormFactor, FormFactor>();

        var uri = new Uri(configuration["ApiSettings:BaseUrl"] ?? hostBaseAddress);
        services.AddScoped(_ => new HttpClient { BaseAddress = uri });
        services.AddScoped<IManagerAuthenticationModeResolver, HttpManagerAuthenticationModeResolver>();
        services.AddLocalStorageServices();
        services.AddAeroHttpClients(uri);
        services.AddAeroCommerceClient();
        services.AddSingleton<IAdminStorage, LocalStorageAdminStorage>();
        services.AddSingleton<AdminStateContainer>();
        services.AddSingleton<AppState>();
        services.AddScoped<ManagerThemeService>();
        services.AddScoped<ManagerAssistantState>();
        services.AddScoped<Aero.Cms.Abstractions.Interfaces.ICurrentSiteAccessor, CurrentSiteAccessor>();
        services.AddScoped<Aero.Cms.Contracts.Abstractions.ICurrentSiteAccessor, CurrentSiteAccessor>();
        services.AddNeoUIPrimitives();
        services.AddNeoUIComponents();
        services.Replace(ServiceDescriptor.Scoped<NeoUI.Blazor.ILocalizer, NeoUiBridgeLocalizer>());
        services.AddRadzenComponents();
        services.AddScoped<IErrorReportingService, ErrorReportingService>();
        services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();
        return services;
    }
}
