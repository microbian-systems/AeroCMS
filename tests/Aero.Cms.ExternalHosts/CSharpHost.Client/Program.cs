using Aero.Cms.Web.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddAeroCmsClient(builder.Configuration, builder.HostEnvironment.BaseAddress);
await builder.Build().RunAsync();
