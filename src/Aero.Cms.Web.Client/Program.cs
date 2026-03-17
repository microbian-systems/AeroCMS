using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the Aero.Cms.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

await builder.Build().RunAsync();
