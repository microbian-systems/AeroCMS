using Aero.Caching.Extensions;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Cms.Modules.Setup.Endpoints;
using Aero.Cms.Modules.Setup.Services;
using Aero.Cms.Core;
using Aero.Cms.Abstractions.Authentication;
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
using AeroDB.Sable;

namespace Aero.Cms.Modules.Setup;


// todo - after setup runs it should autodisable itslf by setting hte Enabled = false and disable the aspnet core FeatureFlag and save to db

/// <summary>
/// Registers the bootstrap-safe setup surface and, once configured, runtime setup and import services.
/// </summary>
[Module(nameof(SetupModule))]
public sealed class SetupModule : AeroModuleBase, IConfigureAeroDB
{
    /// <inheritdoc />
public override string Name => nameof(SetupModule);

    /// <inheritdoc />
public override string Version => AeroConstants.Version;

    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override short Order { get; } = -32768;

    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];

    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["setup", "bootstrap"];

    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["setup", "bootstrap"];

    /// <inheritdoc />
public override Dictionary<string, Uri> Urls { get; } = new()
    {
        ["github"] = new Uri("https://github.com/microbian-systems/aerocms"),
        ["website"] = new Uri($"https://aerocms.io/modules/{nameof(SetupModule)}")
    };

    /// <inheritdoc />
    /// <remarks>
    /// Bootstrap-safe services are always registered. Services that require Identity or
    /// AeroDB are registered only when the state is configured or running.
    /// </remarks>
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
        services.TryAddSingleton(_ => new RuntimeBootstrapReadinessGate(bootstrapState.IsConfiguredMode));
        services.TryAddTransient<SetupGateMiddleware>();
        services.TryAddTransient<RuntimeBootstrapReadinessMiddleware>();
        services.TryAddSingleton<ISecretManager>(sp => DataProtectionCertificateBootstrapper.CreateSecretManager(sp.GetService<IConfiguration>()));

        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, RuntimeBootstrapReadinessStartupFilter>());
        services.AddTransient<IStartupFilter, SetupStatusStartupFilter>();

        if (runtimeMode)
        {
            // These services depend on Identity and AeroDB, which are only available in runtime mode
            services.TryAddScoped<ISetupStateStore, AeroSetupStateStore>();
            services.TryAddScoped<IRecoveryAdministratorAuthority, SetupRecoveryAdministratorAuthority>();
            services.Replace(ServiceDescriptor.Scoped<IManagerAuthenticationModeResolver, ManagerAuthenticationModeResolver>());
            services.TryAddScoped<ISetupIdentityBootstrapper, SetupIdentityBootstrapper>();
            services.AddHostedService<InitialAdminRoleRepairService>();
            services.TryAddScoped<ISetupCompletionService, SeedDatabaseService>();
            services.TryAddScoped<ITranslationImportService, TranslationImportService>();
            services.TryAddTransient<IRuntimeBootstrapInitializer, RuntimeBootstrapInitializer>();
            services.AddTransient<IStartupFilter, TranslationImportStartupFilter>();
            services.AddAeroCaching(false);
        }
    }

    /// <summary>Enables optimistic concurrency for the durable setup singleton.</summary>
    public void Configure(StoreOptions opts)
    {
        var setupState = opts.Schema.For<SetupStateDocument>()
            .TableName(Schemas.Tables.SetupState);
        setupState.UseOptimisticConcurrency = true;
    }

    /// <inheritdoc />
    public void Configure(IServiceProvider? services, StoreOptions opts) => Configure(opts);

    /// <inheritdoc />
    /// <remarks>
    /// This hook currently records discovered modules only while setup is incomplete; it
    /// does not execute persistence or seeding.
    /// </remarks>
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
/// Inserts routing and translation-import endpoint mapping into the runtime application pipeline.
/// </summary>
public sealed class TranslationImportStartupFilter : IStartupFilter
{
    /// <inheritdoc />
public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapTranslationImportEndpoint());
            next(app);
        };
}
