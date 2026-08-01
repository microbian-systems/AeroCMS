using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Grpc;

/// <summary>
/// Registers MagicOnion server services for the gRPC module.
/// </summary>
/// <remarks>
/// This module does not map MagicOnion endpoints or configure transport listeners, TLS, authentication,
/// authorization, interceptors beyond service-level attributes, message sizes, deadlines, retries, or environment
/// gating. Service registration alone does not expose a protocol endpoint.
/// </remarks>
[Module(nameof(GrpcModule))]
public class GrpcModule : AeroModuleBase
{
        /// <summary>
    /// Gets the fixed name used to discover this module.
    /// </summary>
public override string Name => nameof(GrpcModule);
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
    /// Gets the infrastructure and communication discovery categories.
    /// </summary>
public override IReadOnlyList<string> Category => ["Infrastructure", "Communication"];
        /// <summary>
    /// Gets descriptive gRPC/RPC discovery tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["grpc", "api", "communication", "rpc"];

        /// <summary>
    /// Adds MagicOnion server services to the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">Unused configuration.</param>
    /// <param name="env">Unused host environment.</param>
    /// <remarks>Registration is synchronous; registration exceptions propagate and no cancellation token is exposed.</remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddMagicOnion();
    }
}
