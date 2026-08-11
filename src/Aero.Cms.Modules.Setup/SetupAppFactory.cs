using Aero.AppServer.Startup;
using Aero.Cms.Modules.Setup.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoUI.Blazor.Extensions;
using NeoUI.Blazor.Primitives.Extensions;
using Radzen;
using Serilog;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Creates the setup-only web host used before runtime infrastructure is available.
/// </summary>
/// <remarks>
/// This factory creates a lightweight WebApplication that runs during the setup phase.
/// It includes only the services needed for the setup UI and configuration persistence,
/// without the full runtime services (AeroDB, Orleans, Identity, etc.).
/// Service registration is delegated to <see cref="SetupModule.ConfigureServices"/>
/// to eliminate duplication — configure in one place.
/// </remarks>
public static class SetupAppFactory
{
    /// <summary>
    /// Creates and configures a setup-specific <see cref="WebApplication"/>.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the setup host builder.</param>
    /// <param name="earlyConfig">The consuming host's authoritative configuration.</param>
    /// <param name="contentRootPath">The consuming host's content root.</param>
    /// <param name="environmentName">The consuming host's resolved environment name.</param>
    /// <param name="webRootFileProvider">The consuming host's resolved static-web-asset file provider.</param>
    /// <returns>A configured application that has not yet been started.</returns>
    /// <remarks>
    /// Creating the application may create or load a data-protection certificate and key
    /// ring. The setup host intentionally omits runtime database, Orleans, and Identity services.
    /// </remarks>
    public static async Task<WebApplication> CreateSetupAppAsync(
        string[] args,
        IConfiguration earlyConfig,
        string contentRootPath,
        string environmentName,
        IFileProvider webRootFileProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentNullException.ThrowIfNull(webRootFileProvider);
        var webProjectPath = Path.GetFullPath(contentRootPath);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = webProjectPath,
            EnvironmentName = environmentName
        });

        // The outer host has already composed framework, host, and referenced-RCL assets into
        // its web-root provider. Reuse that provider so the temporary setup host can serve the
        // Blazor runtime and _content assets instead of resolving every endpoint under the
        // consuming application's physical wwwroot directory.
        builder.Environment.WebRootFileProvider = webRootFileProvider;

        // The setup host is a temporary UI surface, not a second configuration owner. Consume the
        // exact resolved configuration graph supplied by the application and do not add another
        // appsettings/environment/command-line precedence stack.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddConfiguration(earlyConfig);
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
        services.AddNeoUIPrimitives();
        services.AddNeoUIComponents();

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
    /// Configures the setup host to use the same persistent key ring, certificate, and application name as the runtime host.
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
    /// Configures exception handling, static assets, antiforgery, setup gating, and interactive setup components.
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

        // A reinstallation must not carry the previous database's selected-site cookie.
        // The paired browser local-storage values are removed by setup-handoff.js.
        app.UseSetupSiteSelectionReset();
        
        // The setup host reuses the consuming host's composite web-root provider. Serve that
        // provider through middleware as well as endpoint routing: referenced RCL and framework
        // assets may not have endpoint metadata in the temporary setup application's manifest.
        app.UseStaticFiles();

        // Map fingerprinted static-asset endpoints when the consuming manifest contributes them.
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
