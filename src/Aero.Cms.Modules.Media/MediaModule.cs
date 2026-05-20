using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core;
using Aero.Cms.Core.Models;
using Aero.Cms.Modules.Media.Areas.Api.v1;
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
public class MediaModule : AeroWebModule, IConfigureMarten
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
            .Index(x => x.SiteId)
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
        services.TryAddScoped<IPexelsService, PexelsService>();

        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroMediaActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroMediaActor>(0, "aero"));
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapMediaApi();
        builder.MapFilesApi();

        return Task.CompletedTask;
    }
}
