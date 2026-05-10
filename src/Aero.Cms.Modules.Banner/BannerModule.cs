using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core;
using Aero.Marten;
using Aero.Modular;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Aero.Cms.Modules.Banner;

[Module(nameof(BannerModule))]
public class BannerModule : AeroModuleBase
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

    // <inheritdoc />
    public override void Configure(IServiceProvider services, StoreOptions options)
    {
        base.Configure(services, options);
    }

    // <inheritdoc />
    public override Task RunAsync(IServiceProvider sp)
    {
        return base.RunAsync(sp);
    }
}
