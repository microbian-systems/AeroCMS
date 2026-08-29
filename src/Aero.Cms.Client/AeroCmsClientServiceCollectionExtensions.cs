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
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
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
        services.AddAuthorizationCore(options =>
        {
            // Browser navigation controls visibility only. The host keeps the authoritative
            // role/claim policy on every API endpoint.
            options.AddPolicy("AeroAdmin", policy => policy.RequireAuthenticatedUser());
        });
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        services.AddScoped<ServerAuthenticationStateProvider>();
        services.AddSingleton<IFormFactor, FormFactor>();

        var uri = new Uri(configuration["ApiSettings:BaseUrl"] ?? hostBaseAddress);
        services.AddScoped(_ => new HttpClient { BaseAddress = uri });
        services.AddTransient<BrowserCredentialsHandler>();
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder =>
                // Keep this next to the browser primary handler so retry handlers cannot
                // replace a request without restoring the Fetch credentials option.
                builder.AdditionalHandlers.Add(
                    builder.Services.GetRequiredService<BrowserCredentialsHandler>())));
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

/// <summary>
/// Includes browser-managed authentication and site-selection cookies on Aero CMS
/// typed-client requests. Cookie values remain inaccessible to managed code.
/// </summary>
internal sealed class BrowserCredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
