using Aero.Cms.Abstractions.Actors;
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

namespace Aero.Cms.Modules.Media;

/// <summary>
/// Represents a class for MediaModule.
/// </summary>
[Module(nameof(MediaModule))]
public class MediaModule : AeroWebModule, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(MediaModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["content", "media"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["media", "assets", "cms"];

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions options)
    {
        options.Schema.For<MediaAsset>()
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .Index(x => x.FileName)
            .Index(x => x.Url)
            .Index(x => x.ParentId)
            .Index(x => x.IsFolder)
            .Index(x => x.MimeType);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
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

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapMediaApi();
        builder.MapFilesApi();

        return Task.CompletedTask;
    }
}
