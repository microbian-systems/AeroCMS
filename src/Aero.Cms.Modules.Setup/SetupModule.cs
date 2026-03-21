using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Routing;
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
        var log = sp.GetRequiredService<ILogger<SetupModule>>();

        var allModules = sp.GetServices<IAeroModule>()
            .OrderBy(m => m.Order)
            .ToList();

        log.LogInformation("Setup module initialized with {ModuleCount} discovered modules: {ModuleNames}",
            allModules.Count,
            string.Join(", ", allModules.Select(module => module.Name)));

        await Task.CompletedTask;
    }
}
