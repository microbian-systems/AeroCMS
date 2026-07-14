using Aero.Caching.Extensions;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Cms.Modules.Setup.Endpoints;
using Aero.Cms.Modules.Setup.Services;
using Aero.Cms.Core;
using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Modular;
using Aero.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup;


// todo - after setup runs it should autodisable itslf by setting hte Enabled = false and disable the aspnet core FeatureFlag and save to db

/// <summary>
/// Aero CMS infrastructure setup (database, caching, etc)
/// </summary>
[Module(nameof(SetupModule))]
public sealed class SetupModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(SetupModule);

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;

        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public override short Order { get; } = -32768;

        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["setup", "bootstrap"];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["setup", "bootstrap"];

        /// <summary>
    /// Gets or sets the Urls.
    /// </summary>
public override Dictionary<string, Uri> Urls { get; } = new()
    {
        ["github"] = new Uri("https://github.com/microbian-systems/aerocms"),
        ["website"] = new Uri($"https://aerocms.io/modules/{nameof(SetupModule)}")
    };

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddLocalization();

        var bootstrapState = new AppSettingsBootstrapStateProvider(config ?? new ConfigurationBuilder().Build()).GetState();
        var runtimeMode = bootstrapState.IsConfiguredMode || bootstrapState.IsRunningMode;

        // Note: Setup page is now a Blazor component (Setup.razor) with @page "/setup"
        // The route is discovered via AddAdditionalAssemblies in Program.cs
        services.AddOptions<AeroDbOptions>()
            .BindConfiguration("Aero:Embedded");
        services.TryAddSingleton<IEnvironmentAppSettingsWriter, EnvironmentAppSettingsWriter>();
        services.TryAddSingleton<InfisicalBootstrapSettingsProvider>();
        services.TryAddSingleton<IDataProtectionCertificateSettingsProvider, ConfigurationDataProtectionCertificateSettingsProvider>();
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

        if (runtimeMode)
        {
            // These services depend on Identity and AeroDB, which are only available in runtime mode
            services.TryAddScoped<ISetupStateStore, AeroSetupStateStore>();
            services.TryAddScoped<ISetupIdentityBootstrapper, SetupIdentityBootstrapper>();
            services.AddHostedService<InitialAdminRoleRepairService>();
            services.TryAddScoped<ISetupCompletionService, SeedDatabaseService>();
            services.TryAddScoped<ITranslationImportService, TranslationImportService>();
            services.TryAddTransient<IRuntimeBootstrapInitializer, RuntimeBootstrapInitializer>();
            services.AddTransient<IStartupFilter, TranslationImportStartupFilter>();
            services.AddAeroCaching(false);
        }
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
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

/// <summary>
/// Represents a class for TranslationImportStartupFilter.
/// </summary>
public sealed class TranslationImportStartupFilter : IStartupFilter
{
        /// <summary>
    /// Configure method.
    /// </summary>
public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapTranslationImportEndpoint());
            next(app);
        };
}
