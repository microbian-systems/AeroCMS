using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Models.Entities;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Registers the ASP.NET Core Identity services used by AeroCMS with AeroDB-backed
/// user and role stores keyed by <see cref="long"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registration includes Identity Core, roles, a sign-in manager, the default token
/// provider types, and the AeroDB user and role stores. The resulting stores require
/// an <c>IDocumentStore</c> to be registered by the host.
/// </para>
/// <para>
/// This module does not configure authentication schemes, authorization policies,
/// <see cref="IdentityOptions"/> (including claim mappings and unique-email policy),
/// cookies, data-protection key persistence or sharing, token lifetimes, HTTPS
/// enforcement, or endpoint mappings. Those concerns remain host responsibilities. In
/// particular, registering the default token providers does not by itself establish a
/// durable multi-instance key ring or guarantee email uniqueness.
/// </para>
/// </remarks>
[Module(nameof(IdentityModule))]
public class IdentityModule : AeroWebModule
{
    /// <summary>
    /// Gets the stable module name used for discovery.
    /// </summary>
    public override string Name => nameof(IdentityModule);

    /// <summary>
    /// Gets the version shared by the current AeroCMS release.
    /// </summary>
    public override string Version => AeroConstants.Version;

    /// <summary>
    /// Gets the author shared by AeroCMS modules.
    /// </summary>
    public override string Author => AeroConstants.Author;

    /// <summary>
    /// Gets an empty list because the module declares no module-ordering dependencies.
    /// </summary>
    /// <remarks>
    /// This metadata does not remove the runtime requirement for the host to register
    /// AeroDB and its document store.
    /// </remarks>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>
    /// Gets the categories under which the module is presented.
    /// </summary>
    public override IReadOnlyList<string> Category => ["Identity", "Security"];

    /// <summary>
    /// Gets the discovery tags associated with authentication, users, and roles.
    /// </summary>
    public override IReadOnlyList<string> Tags => ["auth", "identity", "users", "roles"];

    /// <summary>
    /// Adds the long-keyed AeroCMS Identity managers and AeroDB stores to the service
    /// collection.
    /// </summary>
    /// <param name="services">The collection to which the Identity services are added.</param>
    /// <param name="config">
    /// The host configuration. This implementation does not read it.
    /// </param>
    /// <param name="env">
    /// The host environment. This implementation does not read it.
    /// </param>
    /// <remarks>
    /// <para>
    /// The active store registrations are <c>AeroDBUserStore&lt;AeroUser, AeroRole, long&gt;</c>
    /// and <c>AeroDBRoleStore&lt;AeroRole, long&gt;</c>. The similarly named wrapper
    /// types in this assembly are not registered here.
    /// </para>
    /// <para>
    /// Store operations may open independent AeroDB sessions. Callers must not infer
    /// cross-operation atomicity, optimistic-concurrency handling, immediate session
    /// revocation, tenant isolation, or encrypted token storage from this registration.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddIdentityCore<AeroUser>()
            .AddRoles<AeroRole>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddAeroDBStores<AeroUser, AeroRole, long>();
    }
}
