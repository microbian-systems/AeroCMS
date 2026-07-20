using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Jobs;

/// <summary>
/// Declares background-job metadata for Aero CMS module discovery.
/// </summary>
/// <remarks>
/// The project references TickerQ packages, but this module currently registers no scheduler, persistence, cache,
/// telemetry, dashboard, endpoint, or job function. It also defines no triggers, retries, misfire handling,
/// idempotency, concurrency policy, cancellation flow, tenant scope, or authorization. Package references and
/// discovery tags do not guarantee durable scheduling, exactly-once execution, or an active worker.
/// </remarks>
[Module(nameof(JobsModule))]
public class JobsModule : AeroModuleBase
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
    /// Performs no scheduler or job-service registration.
    /// </summary>
    /// <param name="services">The service collection, which this implementation leaves unchanged.</param>
    /// <param name="config">Unused configuration.</param>
    /// <param name="env">Unused host environment.</param>
    /// <remarks>The method is synchronous and defines no cancellation or failure mapping.</remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {

    }



        /// <summary>
    /// Performs no module-builder, dashboard, endpoint, or job registration.
    /// </summary>
    /// <param name="builder">The module builder, which this implementation leaves unchanged.</param>
public override void Configure(IAeroModuleBuilder builder)
    {

    }
}
