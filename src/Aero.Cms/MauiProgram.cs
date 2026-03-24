using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Http.Clients;
using Aero.Cms.Shared.Services;
using Aero.Cms.Services;
using Radzen;

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

        // Add device-specific services used by the Aero.Cms.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        
        // Load API base URL from configuration, fallback to default if not found
        var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:49572";
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
        builder.Services.AddScoped<IBlockService, HttpBlockService>();
        
        // Register API clients
        builder.Services.AddScoped<DocsClient>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddRadzenComponents();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
