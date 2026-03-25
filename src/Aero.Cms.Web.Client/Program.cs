using Aero.Cms.Core.Http.Clients;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Client.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the Aero.Cms.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<IBlockService, HttpBlockService>();

// Register API clients
builder.Services.AddScoped<DocsClient>();
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
