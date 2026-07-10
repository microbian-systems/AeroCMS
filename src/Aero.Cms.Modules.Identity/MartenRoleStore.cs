using Aero.Core.Identity;
using AeroDB;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// AeroDB-backed RoleStore for the Identity module.
/// Wraps <see cref="AeroDBRoleStore{TRole, TKey}"/> with the Aero role type and a long key.
/// </summary>
/// <typeparam name="TRole">The role type, must inherit from <see cref="AeroRole"/>.</typeparam>
public class RoleStore<TRole> : AeroDBRoleStore<TRole, long>
    where TRole : AeroRole, new()
{
    /// <summary>
    /// Constructs a new instance of <see cref="RoleStore{TRole}"/>.
    /// </summary>
    /// <param name="store">The AeroDB document store.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="describer">Identity error describer.</param>
    public RoleStore(
        IDocumentStore store,
        ILogger<RoleStore<TRole>> logger,
        IdentityErrorDescriber? describer = null)
        : base(store, (ILogger)logger, describer)
    {
    }
}
