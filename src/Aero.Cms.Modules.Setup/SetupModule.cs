using Aero.Caching.Extensions;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Cms.Modules.Setup.Endpoints;
using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Modular;
using Aero.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup;


// todo - after setup runs it should autodisable itslf by setting hte Enabled = false and disable the aspnet core FeatureFlag and save to db

/// <summary>
/// Aero CMS infrastructure setup (database, caching, etc)
/// </summary>
public sealed class SetupModule : AeroModuleBase
{
    public override string Name => nameof(SetupModule);

    public override string Version => AeroConstants.Version;

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
        var bootstrapState = new AppSettingsBootstrapStateProvider(config ?? new ConfigurationBuilder().Build()).GetState();
        var runtimeMode = bootstrapState.IsConfiguredMode || bootstrapState.IsRunningMode;

        // Note: Setup page is now a Blazor component (Setup.razor) with @page "/setup"
        // The route is discovered via AddAdditionalAssemblies in Program.cs
        services.AddOptions<AeroDbOptions>()
            .BindConfiguration("Aero:Embedded");
        services.TryAddSingleton<IEnvironmentAppSettingsWriter, EnvironmentAppSettingsWriter>();
        services.TryAddSingleton<InfisicalBootstrapSettingsProvider>();
        services.TryAddSingleton<IDataProtectionCertificateSettingsProvider, ConfigurationDataProtectionCertificateSettingsProvider>();
        services.TryAddTransient<IRuntimeBootstrapInitializer, RuntimeBootstrapInitializer>();
        services.TryAddSingleton<IBootstrapStateProvider, AppSettingsBootstrapStateProvider>();
        services.TryAddScoped<ISetupInitializationService, SetupInitializationService>();
        services.TryAddScoped<IDatabaseBootstrapService, DatabaseBootstrapService>();
        services.TryAddScoped<ICacheBootstrapService, CacheBootstrapService>();
        services.TryAddScoped<IBootstrapCompletionWriter, BootstrapCompletionWriter>();
        services.TryAddScoped<IBootstrapPendingSetupRequestStore, BootstrapPendingSetupRequestStore>();
        services.TryAddScoped<ISetupBootstrapHandoffService, SetupBootstrapHandoffService>();
        services.TryAddSingleton<SetupPathAllowlist>();
        services.TryAddTransient<SetupGateMiddleware>();
        services.TryAddSingleton<ISecretManager>(sp => DataProtectionCertificateBootstrapper.CreateSecretManager(sp.GetService<IConfiguration>()));

        services.AddTransient<IStartupFilter, SetupStatusStartupFilter>();
        services.AddAeroCaching(false);

        if (runtimeMode)
        {
            // These services depend on Identity and Marten, which are only available in runtime mode
            services.TryAddScoped<ISetupStateStore, MartenSetupStateStore>();
            services.TryAddScoped<ISetupIdentityBootstrapper, SetupIdentityBootstrapper>();
            services.TryAddScoped<ISetupCompletionService, SeedDatabaseService>();
            services.TryAddTransient<IRuntimeBootstrapInitializer, RuntimeBootstrapInitializer>();
        }
    }

    public override async Task RunAsync(IServiceProvider sp)
    {
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
