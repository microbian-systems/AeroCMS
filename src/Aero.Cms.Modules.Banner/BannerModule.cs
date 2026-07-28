using Aero.Cms.Core;
using Aero.Modular;
using AeroDB.Sable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Banner;

/// <summary>
/// Represents a class for BannerModule.
/// </summary>
[Module(nameof(BannerModule))]
public class BannerModule : AeroModuleBase, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(BannerModule);

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
public override IReadOnlyList<string> Category => ["infrastructure"];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["web", "infrastructure"];


    // <inheritdoc />
        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<IBannerService, BannerService>();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions options)
    {
        options.Schema.For<BannerModel>()
            .TableName(Schemas.Tables.Banners)
            .Identity(x => x.Id);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }

    // <inheritdoc />
        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IServiceProvider sp)
    {
        return base.RunAsync(sp);
    }
}
