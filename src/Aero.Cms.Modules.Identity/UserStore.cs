using Aero.Models.Entities;
using AeroDB.Sable;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Identity;

/// <inheritdoc cref="AeroDBUserStore{TUser, TRole, TKey}"/>
/// <typeparam name="TUser">The user type, must inherit from <see cref="AeroUser"/>.</typeparam>
/// <typeparam name="TRole">The role type, must inherit from <see cref="AeroRole"/>.</typeparam>
/// <remarks>
/// <para>
/// This convenience wrapper fixes the key type to <see cref="long"/>. It is not
/// registered by <see cref="IdentityModule.ConfigureServices"/>; the active
/// registration resolves the base AeroDB store directly.
/// </para>
/// <para>
/// Operations are governed by the base store and its document sessions. This wrapper
/// adds no cross-call transaction, concurrency, tenant-isolation, token-revocation, or
/// token-at-rest secrecy guarantees.
/// </para>
/// </remarks>
public class UserStore<TUser, TRole> : AeroDBUserStore<TUser, TRole, long>
    where TUser : AeroUser, new()
    where TRole : AeroRole, new()
{
    /// <summary>
    /// Initializes the long-keyed AeroDB user-store wrapper.
    /// </summary>
    /// <param name="store">The document store used by the base implementation.</param>
    /// <param name="logger">The logger forwarded to the base implementation.</param>
    /// <param name="identityOptions">
    /// Optional Identity behavior settings forwarded to the base implementation.
    /// </param>
    /// <param name="describer">
    /// Optional factory for localized Identity error descriptions.
    /// </param>
    /// <remarks>
    /// Constructing this type does not register it with ASP.NET Core Identity.
    /// </remarks>
    public UserStore(
        IDocumentStore store,
        ILogger<UserStore<TUser, TRole>> logger,
        IOptions<IdentityOptions>? identityOptions = null,
        IdentityErrorDescriber? describer = null)
        : base(store, (ILogger)logger, identityOptions, describer)
    {
    }
}
