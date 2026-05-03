using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Services.Images;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Marten;

namespace Aero.Cms.Modules.Media;

[Module(nameof(MediaModule))]
public class MediaModule : AeroModuleBase, IConfigureMarten
{
    public override string Name => nameof(MediaModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public override void Configure(IServiceProvider services, StoreOptions options)
    {
        base.Configure(services, options);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        // Pexels image service — for downloading and storing media assets.
        // Registered here because Media owns the media storage domain.
        services.TryAddScoped<IPexelsService, PexelsService>();
    }
}
