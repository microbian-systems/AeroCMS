using Aero.Cms.Abstractions.Http;
using Aero.Core.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Aero.Cms.Contracts.Abstractions;
using Aero.Cms.Contracts.Services;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Services;
using Aero.Cms.Shared.Services;
using Aero.Cms.Ui.Hyper;
using Aero.Cms.Ui.Neo;
using Aero.Cms.Web.Client.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeoUI.Blazor;
using NeoUI.Blazor.Extensions;
using NeoUI.Blazor.Primitives.Extensions;
using Radzen;
using Aero.Cms.Abstractions.Blocks;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add authentication state provider for InteractiveWebAssembly.
// The ServerAuthenticationStateProvider calls the Identity API's /me endpoint,
// which reads the .AeroCms.Auth cookie sent automatically by the browser.
// This provides AuthenticationState to [Authorize] and AuthorizeView components.
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddScoped<ServerAuthenticationStateProvider>(); // Allow explicit cache invalidation

// Add device-specific services used by the Aero.Cms.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<IBlockService, HttpBlockService>();

// Override HttpClient BaseAddress to the host origin for all Aero typed clients.
// This follows the official Blazor WASM pattern documented at:
// https://learn.microsoft.com/aspnet/core/blazor/call-web-api#typed-httpclient
// On the WASM side, builder.HostEnvironment.BaseAddress (derived from <base href>)
// is the correct server URL. On the server side, config-based fallback is used instead.
var uri = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? builder.HostEnvironment.BaseAddress);
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = uri
});

// Register all Aero HTTP clients
builder.Services.AddLocalStorageServices();
builder.Services.AddAeroHttpClients(uri);

// Client-side state container — replaces per-page HTTP calls for site context
builder.Services.AddSingleton<IAdminStorage, LocalStorageAdminStorage>();
builder.Services.AddSingleton<AdminStateContainer>();
builder.Services.AddSingleton<AppState>();

// Legacy registrations
builder.Services.AddScoped<ManagerThemeService>();
builder.Services.AddSingleton<INeoEditorCatalogProvider, NeoEditorCatalogProvider>();
builder.Services.AddScoped<Aero.Cms.Abstractions.Interfaces.ICurrentSiteAccessor, CurrentSiteAccessor>();
builder.Services.AddScoped<Aero.Cms.Contracts.Abstractions.ICurrentSiteAccessor, CurrentSiteAccessor>();
builder.Services.AddNeoUIPrimitives();
builder.Services.AddNeoUIComponents();
builder.Services.Replace(ServiceDescriptor.Scoped<ILocalizer, NeoUiBridgeLocalizer>());
builder.Services.AddAeroCmsHyperUiBlocks();
builder.Services.AddAeroCmsNeoUiBlocks();
builder.Services.AddSingleton<CannedBlockDefinitionProvider>();
builder.Services.AddSingleton<IPageEditorBlockProvider>(sp => sp.GetRequiredService<CannedBlockDefinitionProvider>());
builder.Services.AddSingleton<IPageEditorDefinitionRegistry, PageEditorDefinitionRegistry>();
builder.Services.AddScoped<IEditorNodeActionProvider, EditorNodeActionProvider>();
builder.Services.AddRadzenComponents();

// Register cross-cutting services that run client-side
builder.Services.AddScoped<IErrorReportingService, ErrorReportingService>();
builder.Services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();

await builder.Build().RunAsync();
