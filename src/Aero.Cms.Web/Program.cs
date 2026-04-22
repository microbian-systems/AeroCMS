using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.ServiceDefaults;
using Aero.Cms.Web.Core.Eextensions;
using Aero.Cms.Web.Services;
using Aero.Cms.Web.Setup;
using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Components;
using Aero.Web.Exceptions;
using Radzen;
using Serilog;
using Serilog.Events;


// Implements a two-stage startup pattern:
// 1. Setup App - Runs first with minimal DI container when bootstrap state is "Setup"
// 2. Main App - Runs after setup completes with full DI container
// 
// This allows the setup wizard to run before database/cache infrastructure is initialized,
// and enables automatic transition without manual restart via IHostApplicationLifetime.StopApplication().



// Configure Serilog early for startup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Aero CMS starting up...");

    // Phase 1: Build early configuration and check bootstrap state
    var earlyConfig = BuildEarlyConfiguration(args);
    var bootstrapState = GetBootstrapState(earlyConfig);

    Log.Information("Bootstrap state: {State}", bootstrapState.State);

    // Phase 2: Run Setup App if needed
    if (bootstrapState.IsSetupMode)
    {
        Log.Information("Setup mode detected. Starting setup application...");
        await RunSetupAppAsync(args, earlyConfig);

        // Re-read configuration after setup app exits
        Log.Information("Setup application completed. Re-reading configuration...");
        earlyConfig = BuildEarlyConfiguration(args);
        bootstrapState = GetBootstrapState(earlyConfig);

        Log.Information("Post-setup bootstrap state: {State}", bootstrapState.State);
    }

    // Phase 3: Create and run Main Application
    if (bootstrapState.IsConfiguredMode || bootstrapState.IsRunningMode)
    {
        Log.Information("Starting main application...");
        await RunMainAppAsync(args, earlyConfig, bootstrapState);
    }
    else
    {
        Log.Error("Invalid bootstrap state after setup: {State}. Expected Configured or Running.", bootstrapState.State);
        throw new InvalidOperationException($"Invalid bootstrap state: {bootstrapState.State}");
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}




static IConfiguration BuildEarlyConfiguration(string[] args)
{
    var configBuilder = new ConfigurationBuilder();

    configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
    configBuilder.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);

    configBuilder.AddEnvironmentVariables();
    configBuilder.AddCommandLine(args);

    return configBuilder.Build();
}


static BootstrapState GetBootstrapState(IConfiguration config)
{
    var provider = new AppSettingsBootstrapStateProvider(config);
    return provider.GetState();
}


static async Task RunSetupAppAsync(string[] args, IConfiguration earlyConfig)
{
    var setupApp = await SetupWebApplication.CreateAsync(args, earlyConfig);

    await setupApp.StartAsync();

    try
    {
        Log.Information("Setup application started. Waiting for setup completion...");
        // Block here until StopApplication() is called by SetupBootstrapHandoffService
        await setupApp.WaitForShutdownAsync();
        Log.Information("Setup application received shutdown signal.");
    }
    finally
    {
        await setupApp.StopAsync();
        Log.Information("Setup application stopped.");
    }
}


static async Task RunMainAppAsync(string[] args, IConfiguration earlyConfig, BootstrapState bootstrapState)
{
    var builder = WebApplication.CreateBuilder(args);
    var services = builder.Services;
    var config = builder.Configuration;
    var env = builder.Environment;

    // Add service defaults
    builder.AddServiceDefaults();

    // Add Aero Application Server (Orleans, Marten, etc.)
    await builder.AddAeroApplicationServer();

    // Add MVC and Razor Components
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

    // Add device-specific services
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
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        };
    });
    services.AddExceptionHandler<AeroGlobalExceptionHandler>();

    // Add full Aero CMS runtime services
    var (_, log) = await builder.AddAeroCmsRuntimeAsync<Program>();

    log.Information("Building main Aero application...");

    var app = builder.Build();

    // Configure middleware pipeline
    app.UseExceptionHandler();
    app.MapDefaultEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
    }
    else
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
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
            typeof(SetupModule).Assembly);

    app.MapAeroCmsEndpoints();

    try
    {
        log.Information("Starting main Aero application host...");
        await app.StartAsync();

        try
        {
            await WaitForRequiredInfrastructureAsync(app, bootstrapState, log);

            log.Information("Initializing runtime services...");
            await app.InitializeAeroAppAsync();

            if (bootstrapState.IsConfiguredMode)
            {
                var initializer = app.Services.GetService<IRuntimeBootstrapInitializer>();
                if (initializer != null)
                {
                    log.Information("Running runtime bootstrap initializer...");
                    await initializer.InitializeAsync();
                    log.Information("Runtime bootstrap initialization completed.");
                }
            }

            await app.WaitForShutdownAsync();
        }
        finally
        {
            await app.StopAsync();
        }
    }
    catch (Exception ex)
    {
        log.Fatal(ex, "Error starting the main Aero CMS application");
        throw;
    }
    finally
    {
        log.Information("Main application exiting");
    }
}

static async Task WaitForRequiredInfrastructureAsync(WebApplication app, BootstrapState bootstrapState, Serilog.ILogger log)
{
    if (!bootstrapState.IsConfiguredMode && !bootstrapState.IsRunningMode)
    {
        return;
    }

    var resolvedInfrastructure = app.Services.GetRequiredService<ResolvedInfrastructureSettings>();
    var startupCoordinator = app.Services.GetRequiredService<IRuntimeStartupCoordinator>();

    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

    log.Information(
        "Waiting for required infrastructure. DatabaseMode={DatabaseMode}, CacheMode={CacheMode}",
        resolvedInfrastructure.DatabaseMode,
        resolvedInfrastructure.CacheMode);

    await startupCoordinator.WaitForInfrastructureAsync(resolvedInfrastructure, cts.Token);
}
