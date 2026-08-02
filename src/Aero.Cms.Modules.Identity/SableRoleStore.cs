using Aero.Models.Entities;
using AeroDB.Sable;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Identity;

/// <inheritdoc cref="AeroDBRoleStore{TRole, TKey}"/>
/// <typeparam name="TRole">The role type, must inherit from <see cref="AeroRole"/>.</typeparam>
/// <remarks>
/// <para>
/// This convenience wrapper fixes the key type to <see cref="long"/>. It is not
/// registered by <see cref="IdentityModule.ConfigureServices"/>; the active
/// registration resolves the base AeroDB role store directly.
/// </para>
/// <para>
/// Operations are governed by the base store and its document sessions. This wrapper
/// adds no cross-call transaction, concurrency, or tenant-isolation guarantees.
/// </para>
/// </remarks>
public class RoleStore<TRole> : AeroDBRoleStore<TRole, long>
    where TRole : AeroRole, new()
{
    /// <summary>
    /// Initializes the long-keyed AeroDB role-store wrapper.
    /// </summary>
    /// <param name="store">The document store used by the base implementation.</param>
    /// <param name="logger">The logger forwarded to the base implementation.</param>
    /// <param name="describer">
    /// Optional factory for localized Identity error descriptions.
    /// </param>
    /// <remarks>
    /// Constructing this type does not register it with ASP.NET Core Identity.
    /// </remarks>
    public RoleStore(
        IDocumentStore store,
        ILogger<RoleStore<TRole>> logger,
        IdentityErrorDescriber? describer = null)
        : base(store, (ILogger)logger, describer)
    {
    }
}
