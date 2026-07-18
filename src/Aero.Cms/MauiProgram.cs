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
/// Represents a class for MauiProgram.
/// </summary>
public static class MauiProgram
{
        /// <summary>
    /// CreateMauiApp method.
    /// </summary>
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
