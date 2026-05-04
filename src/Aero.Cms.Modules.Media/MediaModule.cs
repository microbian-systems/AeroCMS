using Aero.Cms.Core;
using Aero.Cms.Core.Models;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Services.Images;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Marten;

namespace Aero.Cms.Modules.Media;

[Module(nameof(MediaModule))]
public class MediaModule : AeroModuleBase, IConfigureMarten
{
    public override string Name => nameof(MediaModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["content", "media"];
    public override IReadOnlyList<string> Tags => ["media", "assets", "cms"];

    public override void Configure(IServiceProvider services, StoreOptions options)
    {
        base.Configure(services, options);

        options.Schema.For<MediaAsset>()
            .DocumentAlias(Schemas.Tables.Media)
            .Identity(x => x.Id)
            .Index(x => x.FileName)
            .Index(x => x.Url)
            .Index(x => x.ParentId)
            .Index(x => x.IsFolder)
            .Index(x => x.MimeType);
        
        base.Configure<MediaAsset>(services, options);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.TryAddScoped<IMediaRepository, MediaRepository>();
        services.TryAddScoped<IMediaService, MediaService>();

        // Pexels image service — for downloading and storing media assets.
        // Registered here because Media owns the media storage domain.
        services.TryAddScoped<IPexelsService, PexelsService>();
    }
}
