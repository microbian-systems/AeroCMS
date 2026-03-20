using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Aero CMS infrastructure setup (database, caching, etc)
/// </summary>
public sealed class SetupModule : AeroModuleBase
{
    public override string Name => nameof(SetupModule);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["setup", "bootstrap"];

    public override IReadOnlyList<string> Tags => ["setup", "bootstrap"];

    public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
        var scope = builder.ServiceProvider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILogger<SetupModule>>();

        await CreateDatabaseIfNotExists(sp, config, log);

    }

    /// <summary>
    /// 1. Checks for a connection string, if found, creates a db if not exists
    /// 2. Then checks db for entry if setup has completed (modules table for the SetupModule aka self entry)
    /// 3. if not completed scans for modules and runs Modules setup 
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="config"></param>
    /// <param name="log"></param>
    async Task CreateDatabaseIfNotExists(IServiceProvider sp, IConfiguration config, ILogger log)
    {
        var connString = config.GetConnectionString("DefaultConnection");

        log.LogInformation("located connection string: {a}", connString);

        await Task.CompletedTask;
    }
}