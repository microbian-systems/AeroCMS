using Aero.Cms.Core;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
    public override short Order { get; } = -32768;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["setup", "bootstrap"];

    public override IReadOnlyList<string> Tags => ["setup", "bootstrap"];

    public override Dictionary<string, Uri> Urls { get; } = new()
    {
        ["github"] = new Uri("https://github.com/microbian-systems/aerocms"),
        ["website"] = new Uri($"https://aerocms.io/modules/{nameof(SetupModule)}")
    };

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.Configure<RazorPagesOptions>(options =>
            options.Conventions.AddAreaPageRoute("MyFeature", "/Setup", SetupPathAllowlist.SetupPath));
        services.TryAddScoped<ISetupStateStore, MartenSetupStateStore>();
        services.TryAddScoped<ISetupInitializationService, SetupInitializationService>();
        services.TryAddScoped<ISetupIdentityBootstrapper, SetupIdentityBootstrapper>();
        services.TryAddScoped<ISetupCompletionService, SetupCompletionService>();
        services.TryAddScoped<IModuleStateStore, ModuleStateStore>();
        services.TryAddSingleton<SetupPathAllowlist>();
        services.TryAddTransient<SetupGateMiddleware>();
    }

    public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
        var scope = builder.ServiceProvider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var log = sp.GetRequiredService<ILogger<SetupModule>>();
        var setupInitService = sp.GetRequiredService<ISetupInitializationService>();

        // Skip if setup is already complete - prevents unnecessary work on subsequent starts
        if (await setupInitService.IsSetupCompleteAsync())
        {
            log.LogInformation("Setup module skipped - setup already complete");
            return;
        }

        var allModules = sp.GetServices<IAeroModule>()
            .OrderBy(m => m.Order)
            .ToList();

        log.LogInformation("Setup module initialized with {ModuleCount} discovered modules: {ModuleNames}",
            allModules.Count,
            string.Join(", ", allModules.Select(module => module.Name)));

        await Task.CompletedTask;
    }
}
