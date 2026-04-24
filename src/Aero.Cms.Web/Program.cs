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
using System.IO;
using System.Text.Json;


// Implements a two-stage startup pattern:
// 1. Setup App - Runs first with minimal DI container when bootstrap state is "Setup"
// 2. Main App - Runs after setup completes with full DI container
// 
// This allows the setup wizard to run before database/cache infrastructure is initialized,
// and enables automatic transition without manual restart via IHostApplicationLifetime.StopApplication().



// Configure Serilog early for startup logging
var webProjectPath = Aero.Cms.Modules.Setup.Configuration.AppSettingsPathResolver.GetWebProjectPath();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.File(
        Path.Combine(webProjectPath, "logs", "aero-.log"),
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        buffered: true,
        flushToDiskInterval: TimeSpan.FromSeconds(15))
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Aero CMS starting up...");

    // Phase 1: Build early configuration and check bootstrap state
    var earlyConfig = BuildEarlyConfiguration(args, webProjectPath);
    var bootstrapState = GetBootstrapState(earlyConfig);

    Log.Information("Bootstrap state: {State}", bootstrapState.State);

    // Phase 2: Run Setup App if needed
    if (bootstrapState.IsSetupMode)
    {
        Log.Information("Setup mode detected. Starting setup application...");
        await RunSetupAppAsync(args, earlyConfig);

        // Re-read configuration after setup app exits
        Log.Information("Setup application completed. Re-reading configuration...");
        (earlyConfig, bootstrapState) = await ReloadBootstrapStateAfterSetupAsync(args, webProjectPath);

        Log.Information("Post-setup bootstrap state: {State}", bootstrapState.State);
    }

    // Phase 3: Create and run Main Application
    if (bootstrapState.IsConfiguredMode || bootstrapState.IsRunningMode)
    {
        Log.Information("Starting main application...");
        await RunMainAppAsync(args, webProjectPath, earlyConfig, bootstrapState);
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
}
finally
{
    Log.CloseAndFlush();
}




static IConfiguration BuildEarlyConfiguration(string[] args, string webProjectPath)
{
    var configBuilder = new ConfigurationBuilder();

    configBuilder.SetBasePath(webProjectPath);
    configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
    configBuilder.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);

    configBuilder.AddEnvironmentVariables();
    configBuilder.AddCommandLine(args);

    return configBuilder.Build();
}

static async Task<(IConfiguration Config, BootstrapState State)> ReloadBootstrapStateAfterSetupAsync(string[] args, string webProjectPath)
{
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
    var envPath = Path.Combine(webProjectPath, $"appsettings.{env}.json");

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        var config = BuildEarlyConfiguration(args, webProjectPath);
        var state = GetBootstrapState(config);

        Log.Information(
            "Bootstrap reread attempt {Attempt}. Environment={Environment}, File={FilePath}, State={State}",
            attempt,
            env,
            envPath,
            state.State);

        if (!state.IsSetupMode)
        {
            return (config, state);
        }

        if (File.Exists(envPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(envPath);
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.TryGetProperty("AeroCms", out var aeroCms) &&
                    aeroCms.TryGetProperty("Bootstrap", out var bootstrap) &&
                    bootstrap.TryGetProperty("State", out var rawState))
                {
                    Log.Warning(
                        "Bootstrap file still reports State={RawState} on reread attempt {Attempt}.",
                        rawState.GetString(),
                        attempt);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed reading bootstrap file directly during reread attempt {Attempt}.", attempt);
            }
        }

        await Task.Delay(200);
    }

    var finalConfig = BuildEarlyConfiguration(args, webProjectPath);
    return (finalConfig, GetBootstrapState(finalConfig));
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


static async Task RunMainAppAsync(string[] args, string webProjectPath, IConfiguration earlyConfig, BootstrapState bootstrapState)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = webProjectPath,
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
    });
    var services = builder.Services;
    var config = builder.Configuration;
    var env = builder.Environment;

    // Add service defaults
    builder.AddServiceDefaults();

    // Add Aero Application Server (Orleans, Marten, etc.)
    await builder.AddAeroApplicationServer();

    // Keep the web data layer aligned with the resolved infrastructure settings.
    // AddAeroDataLayer currently reads ConnectionStrings:aero from configuration,
    // so stamp the resolved value back into configuration before runtime services register.
    var resolvedInfrastructure = new InfrastructureConnectionStringResolver(config).Resolve();
    config["ConnectionStrings:aero"] = resolvedInfrastructure.DatabaseConnectionString;

    if (!string.IsNullOrWhiteSpace(resolvedInfrastructure.CacheConnectionString))
    {
        config["ConnectionStrings:cache"] = resolvedInfrastructure.CacheConnectionString;
    }

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

    log.Information("Building main Aero CMS app...");

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

            log.Information("Applying runtime preparation...");
            await app.PrepareAeroAppAsync();

            if (bootstrapState.IsConfiguredMode)
            {
                await using var runtimeBootstrapScope = app.Services.CreateAsyncScope();
                var initializer = runtimeBootstrapScope.ServiceProvider.GetService<IRuntimeBootstrapInitializer>();
                if (initializer != null)
                {
                    log.Information("Running runtime bootstrap initializer...");
                    await initializer.InitializeAsync();
                    log.Information("Runtime bootstrap initialization completed.");
                }
            }

            log.Information("Initializing runtime services...");
            await app.InitializeAeroAppAsync();

            await app.WaitForShutdownAsync();
        }
        catch (Exception ex) when (bootstrapState.IsConfiguredMode)
        {
            await TryMarkBootstrapFailedAsync(app, log);
            throw;
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

static async Task TryMarkBootstrapFailedAsync(WebApplication app, Serilog.ILogger log)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetService<IBootstrapCompletionWriter>();

        if (writer != null)
        {
            await writer.MarkFailedAsync();
            log.Warning("Bootstrap state marked as Failed.");
        }
    }
    catch (Exception markFailedEx)
    {
        log.Error(markFailedEx, "Failed to persist bootstrap Failed state.");
    }
}
