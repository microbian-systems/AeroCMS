using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Web.Bootstrap;
using Aero.Cms.Web.Components;
using Aero.Cms.Web.Generated;
using Serilog;

var webProjectPath = Aero.Cms.Modules.Setup.Configuration.AppSettingsPathResolver.GetWebProjectPath();
AeroCmsExtensions.ConfigureAeroCmsBootstrapLogging(webProjectPath);

try
{
    var earlyResult = await AeroStartupPipeline.RunEarlyPhasesAsync(args);

    if (earlyResult is not { } result)
    {
        return;
    }

    if (result.State.IsConfiguredMode || result.State.IsRunningMode)
    {
        Log.Information("Starting main application...");
        await RunMainAppAsync(args, result.WebProjectPath, result.State);
    }
    else if (!result.State.IsSetupMode)
    {
        Log.Error("Invalid bootstrap state after early phases: {State}. Expected Configured or Running.", result.State);
        throw new InvalidOperationException($"Invalid bootstrap state: {result.State}");
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

static async Task RunMainAppAsync(string[] args, string webProjectPath, BootstrapState bootstrapState)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = webProjectPath,
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
    });

    var (_, log) = await builder.AddAeroCmsAsync<Program>(options =>
    {
        options.ModuleDescriptors = GeneratedAeroModuleCatalog.Descriptors;
        options.ConfigureWolverine = GeneratedWolverineHandlerCatalog.Register;
        options.ConfigureGrains = GeneratedAeroGrainCatalog.Register;
    });

    var app = builder.Build();
    await app.RunAeroCmsAsync<App>(
        bootstrapState,
        log,
        components => components.AddAdditionalAssemblies(typeof(Aero.Cms.Web.Client._Imports).Assembly));
}
