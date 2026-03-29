using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Site alias management module for handling URL aliases and redirects.
/// </summary>
public class AliasModule : AeroModuleBase
{
    public override string Name => nameof(AliasModule);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => [];

    public override IReadOnlyList<string> Tags => [];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        return base.RunAsync(builder);
    }
}