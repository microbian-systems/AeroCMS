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
/// Registers media persistence, services, actor access, and administrative endpoints.
/// </summary>
[Module(nameof(MediaModule))]
public class MediaModule : AeroWebModule, IConfigureAeroDB
{
    /// <inheritdoc />
public override string Name => nameof(MediaModule);
    /// <inheritdoc />
public override string Version => AeroConstants.Version;
    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["content", "media"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["media", "assets", "cms"];

    /// <summary>
    /// Configures the <see cref="MediaAsset"/> document identity and query indexes.
    /// </summary>
    /// <param name="options">The AeroDB store options being assembled.</param>
public void Configure(StoreOptions options)
    {
        options.Schema.For<MediaAsset>()
            .TableName(Schemas.Tables.MediaAssets)
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .Index(x => x.FileName)
            .Index(x => x.Url)
            .Index(x => x.ParentId)
            .Index(x => x.IsFolder)
            .Index(x => x.MimeType);

        options.Schema.For<CmsFile>()
            .TableName(Schemas.Tables.Files)
            .Identity(x => x.Id);
    }

    /// <summary>
    /// Configures media persistence using the service-provider-aware AeroDB hook.
    /// </summary>
    /// <param name="services">The built service provider; not used by this module.</param>
    /// <param name="options">The AeroDB store options being assembled.</param>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Repository, service, and Pexels registrations are added only when no earlier registration
    /// exists. The media actor is resolved as singleton grain key <c>0</c> with key extension
    /// <c>aero</c>; individual operations carry their own media and site identifiers.
    /// </remarks>
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

    /// <inheritdoc />
    /// <remarks>
    /// Maps both media and general-file admin groups. Those mapping methods do not add an
    /// authorization policy themselves, so the host must protect the admin route boundary.
    /// </remarks>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapMediaApi();
        builder.MapFilesApi();

        return Task.CompletedTask;
    }
}
