using Aero.Cms.Modules.Setup;
using Aero.Cms.ServiceDefaults;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Services;
using Aero.Cms.Core.Extensions;
using Radzen;
using Aero.AppServer;
using Aero.Cms.Web.Core.Eextensions;
using Aero.Cms.Web.Components;
using Aero.Web.Exceptions;
using Aero.Cms.Modules.Setup.Bootstrap;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;
var env = builder.Environment;

var bootstrapSection = config.GetSection("AeroCms:Bootstrap");
var bootstrapState = bootstrapSection["State"];
if (string.IsNullOrWhiteSpace(bootstrapState))
{
    var setupComplete = bootstrapSection.GetValue<bool?>("SetupComplete") ?? false;
    var seedComplete = bootstrapSection.GetValue<bool?>("SeedComplete") ?? false;
    bootstrapState = setupComplete && seedComplete
        ? BootstrapStates.Running
        : bootstrapSection.Exists()
            ? BootstrapStates.Configured
            : BootstrapStates.Setup;
}

var runtimeMode = string.Equals(bootstrapState, BootstrapStates.Configured, StringComparison.OrdinalIgnoreCase)
    || string.Equals(bootstrapState, BootstrapStates.Running, StringComparison.OrdinalIgnoreCase);

// Always add application server - InfrastructureConnectionStringResolver returns embedded defaults in Setup mode
// This enables in-process runtime activation after setup configuration
await builder.AddAeroApplicationServer();


builder.AddServiceDefaults();


// Add services to the container.
services.AddControllersWithViews();
services.AddAuthentication();
services.AddAuthorization();
services.AddRadzenComponents();
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

// Load API base URL from configuration
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:333";
builder.Configuration["AeroHttpClientBaseAddress"] = apiBaseUrl;

// Register all Aero HTTP clients
services.AddAeroHttpClients(config);
services.AddScoped<ManagerThemeService>();
services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});
services.AddExceptionHandler<AeroGlobalExceptionHandler>();

var (_, log) = runtimeMode
    ? await builder.AddAeroCmsRuntimeAsync<Program>()
    : await builder.AddAeroCmsBootstrapAsync<Program>();

if (!runtimeMode)
{
    new SetupModule().ConfigureServices(services, config, env);
}


log.Information("building aero application services");


var app = builder.Build();

app.UseExceptionHandler();

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
        typeof(Aero.Cms.Web.Client._Imports).Assembly,
        typeof(SetupModule).Assembly);  // Include Setup module for Blazor routing

try
{
    log.Information("starting aero application...");
    if (runtimeMode)
    {
        var initializer = app.Services.GetService<IRuntimeBootstrapInitializer>();
        if (initializer != null)
        {
            await initializer.InitializeAsync();
        }
    }
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
