using Aero.Cms.Core;
using Aero.Modular;
using AeroDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Banner;

[Module(nameof(BannerModule))]
public class BannerModule : AeroModuleBase, IConfigureAeroDB
{
    public override string Name => nameof(BannerModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["infrastructure"];

    public override IReadOnlyList<string> Tags => ["web", "infrastructure"];


    // <inheritdoc />
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<IBannerService, BannerService>();
    }

    public void Configure(StoreOptions options)
    {
    }

    public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }

    // <inheritdoc />
    public override Task RunAsync(IServiceProvider sp)
    {
        return base.RunAsync(sp);
    }
}
