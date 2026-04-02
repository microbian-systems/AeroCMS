using Aero.Cms.Core.Http.Clients;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Client.Services;
using Radzen;
using Aero.Cms.Abstractions.Blocks;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the Aero.Cms.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<IBlockService, HttpBlockService>();

// Register all Aero HTTP clients
builder.Configuration["AeroHttpClientBaseAddress"] = builder.HostEnvironment.BaseAddress + "api/v1";
builder.Services.AddAeroHttpClients(builder.Configuration);

// Legacy registrations
builder.Services.AddScoped<ManagerThemeService>();
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
