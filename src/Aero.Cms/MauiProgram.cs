using Microsoft.Extensions.Logging;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Shared.Services;
using Aero.Cms.Services;

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

        // Add device-specific services used by the Aero.Cms.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5000") }); // todo - get from config
        builder.Services.AddScoped<IBlockService, HttpBlockService>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
