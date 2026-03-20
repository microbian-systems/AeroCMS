using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Marten;
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
        var log = sp.GetRequiredService<ILogger<SetupModule>>(); // serilog log
        var session = sp.GetRequiredService<IDocumentSession>();

        var existing = await session.Query<IAeroModule>()
            .ToListAsync();

        var allModules = sp.GetServices<IAeroModule>()
            .OrderBy(m => m.Order)
            .ToList();

        // Get modules that don't exist yet (by some unique property like Name or Id)
        var newModules = allModules
            .ExceptBy(existing.Select(e => e.Name), m => m.Name)
            .ToList();
    }
}