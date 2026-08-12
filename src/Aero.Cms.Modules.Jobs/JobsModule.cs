using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Abstractions.Content.Importing;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Jobs;

/// <summary>
/// Registers durable content-import job coordination for Aero CMS module discovery.
/// </summary>
/// <remarks>
/// The module registers the durable job store and scoped coordinator. The polling background worker remains an
/// explicit host opt-in through <see cref="ContentImportServiceCollectionExtensions.AddAeroCmsContentImportWorker"/>.
/// </remarks>
[Module(nameof(JobsModule))]
public class JobsModule : AeroModuleBase, IConfigureAeroDB
{
    /// <summary>
    /// Gets the fixed name used to discover this module.
    /// </summary>
    public override string Name => nameof(JobsModule);
    /// <summary>
    /// Gets the Aero CMS version reported by this module.
    /// </summary>
    public override string Version => AeroConstants.Version;
    /// <summary>
    /// Gets the Aero CMS author metadata reported by this module.
    /// </summary>
    public override string Author => AeroConstants.Author;
    /// <summary>
    /// Gets an empty module dependency list.
    /// </summary>
    public override IReadOnlyList<string> Dependencies => [];
    /// <summary>
    /// Gets the infrastructure and background-task discovery categories.
    /// </summary>
    public override IReadOnlyList<string> Category => ["Infrastructure", "BackgroundTasks"];
    /// <summary>
    /// Gets descriptive job, queue, and scheduler discovery tags.
    /// </summary>
    public override IReadOnlyList<string> Tags => ["jobs", "background", "queue", "scheduler"];

    /// <summary>
    /// Registers the durable content-import job store and coordinator.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="config">Optional host configuration.</param>
    /// <param name="env">Optional host environment.</param>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IContentImportJobStore, SableContentImportJobStore>();
        services.AddScoped<IContentImportCoordinator, ContentImportCoordinator>();
    }

    /// <summary>
    /// Performs no module-builder, dashboard, endpoint, or job registration.
    /// </summary>
    /// <param name="builder">The module builder, which this implementation leaves unchanged.</param>
    public override void Configure(IAeroModuleBuilder builder)
    {

    }

    public void Configure(StoreOptions options)
    {
        var mapping = options.Schema.For<ContentImportJobDocument>();
        mapping.TableName("content_import_jobs");
        mapping.Identity(x => x.Id);
        mapping.Index(x => x.TenantId);
        mapping.Index(x => x.SiteId);
        mapping.Index(x => x.State);
        mapping.Index(x => x.LeaseExpiresOn);
        mapping.UniqueIndex(x => x.RequestIdentity);
        mapping.UseOptimisticConcurrency = true;
    }

    public void Configure(IServiceProvider? services, StoreOptions options) => Configure(options);
}

/// <summary>Explicit opt-in for the scoped durable content-import worker.</summary>
public static class ContentImportServiceCollectionExtensions
{
    public static IServiceCollection AddAeroCmsContentImportWorker(this IServiceCollection services)
    {
        services.AddHostedService<ContentImportBackgroundService>();
        return services;
    }
}
