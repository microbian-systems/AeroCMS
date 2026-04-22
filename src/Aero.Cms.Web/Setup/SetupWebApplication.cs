using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Components;
using Aero.Cms.Web.Services;
using Aero.Secrets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radzen;
using Serilog;

namespace Aero.Cms.Web.Setup;

/// <summary>
/// Factory class for creating the setup-specific WebApplication with minimal services.
/// </summary>
/// <remarks>
/// This factory creates a lightweight WebApplication that runs during the setup phase.
/// It includes only the services needed for the setup UI and configuration persistence,
/// without the full runtime services (Marten, Orleans, Identity, etc.).
/// </remarks>
public static class SetupWebApplication
{
    /// <summary>
    /// Creates and configures a setup-specific WebApplication.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="earlyConfig">Early configuration for bootstrap state checking.</param>
    /// <returns>Configured WebApplication ready to start.</returns>
    public static async Task<WebApplication> CreateAsync(string[] args, IConfiguration earlyConfig)
    {
        var builder = WebApplication.CreateBuilder(args);
        var services = builder.Services;
        var config = builder.Configuration;
        var env = builder.Environment;

        // Configure Data Protection with shared settings (same as main app will use)
        ConfigureDataProtection(services, config);

        // Add minimal logging
        services.AddLogging(logging =>
        {
            logging.AddSerilog();
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.AddTransient<IFormFactor, FormFactor>();

        // Add Razor Components for setup UI
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add Radzen components
        services.AddRadzenComponents();

        // Add memory cache for bootstrap operations
        services.AddMemoryCache();

        // Add HTTP context accessor for setup operations
        services.AddHttpContextAccessor();

        // Register bootstrap-safe setup services
        RegisterBootstrapServices(services, config);

        // Configure minimal middleware pipeline
        var app = builder.Build();
        ConfigureSetupPipeline(app);

        return app;
    }

    /// <summary>
    /// Configures Data Protection with shared settings that will be identical in the main app.
    /// </summary>
    private static void ConfigureDataProtection(IServiceCollection services, IConfiguration config)
    {
        var settings = DataProtectionCertificateBootstrapper.ResolveSettings(config);
        var certificate = DataProtectionCertificateBootstrapper.GetOrCreateCertificate(settings);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(settings.KeyRingPath))
            .ProtectKeysWithCertificate(certificate)
            .SetApplicationName(settings.ApplicationName);
    }

    /// <summary>
    /// Registers bootstrap-safe services that don't require runtime dependencies.
    /// </summary>
    private static void RegisterBootstrapServices(IServiceCollection services, IConfiguration config)
    {
        // Configuration and state providers
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IBootstrapStateProvider, AppSettingsBootstrapStateProvider>();
        services.AddSingleton<IEnvironmentAppSettingsWriter, EnvironmentAppSettingsWriter>();
        services.AddSingleton<IDataProtectionCertificateSettingsProvider, ConfigurationDataProtectionCertificateSettingsProvider>();
        services.AddSingleton<InfisicalBootstrapSettingsProvider>();

        // Secret management
        services.AddSingleton<ISecretManager>(sp => 
            DataProtectionCertificateBootstrapper.CreateSecretManager(sp.GetService<IConfiguration>()));

        // Bootstrap services
        services.AddScoped<IDatabaseBootstrapService, DatabaseBootstrapService>();
        services.AddScoped<ICacheBootstrapService, CacheBootstrapService>();
        services.AddScoped<IBootstrapPendingSetupRequestStore, BootstrapPendingSetupRequestStore>();
        services.AddScoped<IBootstrapCompletionWriter, BootstrapCompletionWriter>();

        // Setup handoff service - triggers shutdown after setup completion
        services.AddScoped<ISetupBootstrapHandoffService, SetupBootstrapHandoffService>();

        // Setup initialization service
        services.AddScoped<ISetupInitializationService, SetupInitializationService>();

        // Setup allowlist for path gating
        services.AddSingleton<SetupPathAllowlist>();
        services.AddTransient<SetupGateMiddleware>();
    }

    /// <summary>
    /// Configures the minimal middleware pipeline for the setup app.
    /// </summary>
    private static void ConfigureSetupPipeline(WebApplication app)
    {
        // Exception handling
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        // HTTPS redirection
        app.UseHttpsRedirection();

        // Static files
        app.UseStaticFiles();

        // Routing
        app.UseRouting();

        // Antiforgery
        app.UseAntiforgery();

        // Setup gate middleware - ensures only setup paths are accessible
        app.UseCmsSetupGate();

        // Map Razor Components
        app.MapRazorComponents<SetupApp>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(SetupModule).Assembly);
    }
}
