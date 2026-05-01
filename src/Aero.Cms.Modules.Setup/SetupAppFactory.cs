using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radzen;
using Serilog;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Factory class for creating the setup-specific WebApplication with minimal services.
/// </summary>
/// <remarks>
/// This factory creates a lightweight WebApplication that runs during the setup phase.
/// It includes only the services needed for the setup UI and configuration persistence,
/// without the full runtime services (Marten, Orleans, Identity, etc.).
/// Service registration is delegated to <see cref="SetupModule.ConfigureServices"/>
/// to eliminate duplication — configure in one place.
/// </remarks>
public static class SetupAppFactory
{
    /// <summary>
    /// Creates and configures a setup-specific WebApplication.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="earlyConfig">Early configuration for bootstrap state checking.</param>
    /// <returns>Configured WebApplication ready to start.</returns>
    public static async Task<WebApplication> CreateSetupAppAsync(string[] args, IConfiguration earlyConfig)
    {
        var webProjectPath = AppSettingsPathResolver.GetWebProjectPath();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = webProjectPath,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
        });
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

        // Add Razor Components for setup UI
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add Radzen components
        services.AddRadzenComponents();

        // Add memory cache for bootstrap operations
        services.AddMemoryCache();

        services.AddAntiforgery(options =>
        {
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Allow HTTP in dev
        });

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.Secure = CookieSecurePolicy.SameAsRequest;
        });

        // Add HTTP context accessor for setup operations
        services.AddHttpContextAccessor();

        // Register all bootstrap-safe setup services via SetupModule.
        // This avoids duplicating the 12+ registrations that were previously
        // in RegisterBootstrapServices() — configure in one source of truth.
        var setupModule = new SetupModule();
        setupModule.ConfigureServices(services, config, env);

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
    /// Configures the minimal middleware pipeline for the setup app.
    /// </summary>
    private static void ConfigureSetupPipeline(WebApplication app)
    {
        // Exception handling
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        
        // Static assets must be mapped before Antiforgery/Routing
        app.MapStaticAssets();
        
        app.UseAntiforgery();

        // Setup gate middleware - ensures only setup paths are accessible
        app.UseCmsSetupGate();

        // Map Razor Components.
        // NOTE: Do NOT call AddAdditionalAssemblies with SetupRoot's own assembly —
        // MapRazorComponents already registers the root component's assembly automatically.
        app.MapRazorComponents<Areas.Setup.Pages.SetupRoot>()
            .AddInteractiveServerRenderMode();
    }
}
