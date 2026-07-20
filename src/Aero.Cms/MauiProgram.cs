using System.Reflection;
using Microsoft.Extensions.Configuration;
using Aero.Cms.Shared.Services;
using Aero.Cms.Services;
using Radzen;
using Serilog;
using Serilog.Events;
using Aero.Cms.Abstractions.Http;
using NeoUI.Blazor.Primitives.Extensions;
using NeoUI.Blazor.Extensions;

namespace Aero.Cms;

/// <summary>
/// Builds the MAUI Hybrid host, logging pipeline, HTTP clients, and shared UI services.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates the configured MAUI application service provider.
    /// </summary>
    /// <returns>The built application.</returns>
    /// <remarks>
    /// Embedded <c>appsettings.json</c> is optional. API clients use its
    /// <c>ApiSettings:BaseUrl</c> value when present, while the directly registered
    /// <see cref="HttpClient"/> falls back to <c>https://localhost:333</c>. Serilog writes
    /// rolling files beneath the platform application-data directory and is disposed by logging.
    /// </remarks>
    /// <exception cref="UriFormatException">A configured API base URL is not a valid absolute or relative URI.</exception>
    /// <exception cref="ArgumentException">The configured value is relative and cannot be assigned as an HTTP base address.</exception>
public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Load appsettings.json from embedded resources
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Aero.Cms.appsettings.json");
        if (stream != null)
        {
            builder.Configuration.AddJsonStream(stream);
        }

        // Configure Serilog
        var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "aero-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            //.WriteTo.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Logging.AddSerilog(dispose: true);

        // Add device-specific services used by the Aero.Cms.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:333")
        });
        
        // Register all Aero HTTP clients
        var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];
        builder.Services.AddAeroHttpClients(baseUrl is not null ? new Uri(baseUrl) : null);
        
        // Legacy registrations (ensure both class and interface work for transition)
        builder.Services.AddScoped<ManagerThemeService>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddRadzenComponents();
        builder.Services.AddNeoUIPrimitives();
        builder.Services.AddNeoUIComponents();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        return builder.Build();
    }
}
