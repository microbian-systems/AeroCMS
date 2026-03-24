using Aero.Cms.Modules.Setup;
using Aero.Cms.ServiceDefaults;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Components;
using Aero.Cms.Web.Core.Modules;
using Aero.Cms.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;
var env = builder.Environment;

builder.AddServiceDefaults();

// Add services to the container.
services.AddControllersWithViews();
services.AddAuthentication();
services.AddAuthorization();
services.AddRazorPages()
    .AddApplicationPart(typeof(SetupModule).Assembly)
    .AddApplicationPart(typeof(Aero.Cms.Modules.Docs.DocsModule).Assembly)
    .AddApplicationPart(typeof(Aero.Cms.Core.Blocks.BlockBase).Assembly);
services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();


services.AddCascadingAuthenticationState();

// Add device-specific services used by the Aero.Cms.Shared project
services.AddSingleton<IFormFactor, FormFactor>();


var (_, log) = await builder.AddAeroCmsAsync<Program>();


log.Information("building aero application services");
var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapStaticAssets();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseCmsSetupGate();
app.UseAntiforgery();

app.MapRazorPages();


app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(Aero.Cms.Shared._Imports).Assembly,
        typeof(Aero.Cms.Web.Client._Imports).Assembly);

try
{
    log.Information("starting aero application...");
    await app.MapAeroAppAsync();
    app.Run();
}
catch (Exception ex)
{
    log.Fatal(ex, "error starting the aero cms application");
}
finally
{
    log.Information("exiting application");
}

