using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SimpleSecurity;

/// <summary>
/// Supplies discovery metadata for the Simple Security module.
/// </summary>
/// <remarks>
/// This module does not configure authentication or authorization and does not
/// handle credentials, tokens, sessions, cookies, users, or tenants. It also
/// defines no storage, hashing, encryption, middleware, or endpoints. Loading
/// the module therefore establishes no application security boundary.
/// </remarks>
[Module(nameof(SimpleSecurityModule))]
public class SimpleSecurityModule : AeroModuleBase
{
        /// <summary>
    /// The stable module identifier, <c>SimpleSecurityModule</c>.
    /// </summary>
public override string Name => nameof(SimpleSecurityModule);
        /// <summary>
    /// The Aero CMS version reported in module metadata.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// The Aero CMS author reported in module metadata.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// An empty collection because the module declares no module-ordering dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// The security category used to classify this module.
    /// </summary>
public override IReadOnlyList<string> Category => ["Security"];
        /// <summary>
    /// The security, simple, and authentication discovery tags assigned to this module.
    /// </summary>
    /// <remarks>
    /// These values are metadata only and do not enable authentication or
    /// authorization behavior.
    /// </remarks>
public override IReadOnlyList<string> Tags => ["security", "simple", "auth"];

        /// <inheritdoc />
    /// <remarks>
    /// This override is a no-op: it does not inspect the configuration or
    /// environment and registers no authentication schemes, authorization
    /// policies, handlers, credential stores, or cryptographic services.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
    }

        /// <inheritdoc />
    /// <remarks>
    /// This override makes no changes to the module builder and adds no
    /// middleware or endpoints.
    /// </remarks>
public override void Configure(IAeroModuleBuilder builder)
    {
    }
}
