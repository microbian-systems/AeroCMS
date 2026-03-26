using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Http.Clients;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Shared.Services;
using Aero.Cms.Services;
using Radzen;
using Serilog;
using Serilog.Events;

namespace Aero.Cms;

public static class MauiProgram
{
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
        
        // Load API base URL from configuration, fallback to default if not found
        var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:333";
        builder.Configuration["AeroHttpClientBaseAddress"] = apiBaseUrl;
        
        // Register all Aero HTTP clients
        builder.Services.AddAeroHttpClients(builder.Configuration);
        
        builder.Services.AddScoped<IBlockService, HttpBlockService>();
        
        // Legacy registrations (ensure both class and interface work for transition)
        builder.Services.AddScoped<ManagerThemeService>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddRadzenComponents();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        return builder.Build();
    }
}
