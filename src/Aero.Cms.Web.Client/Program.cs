using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Client.Services;
using Radzen;
using Aero.Cms.Abstractions.Blocks;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

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
builder.Services.AddAeroHttpClients(uri);

// Legacy registrations
builder.Services.AddScoped<ManagerThemeService>();
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
