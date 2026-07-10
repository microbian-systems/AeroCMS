using Aero.Core.Identity;
using Aero.Models.Entities;
using AeroDB;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// AeroDB-backed UserStore for the Identity module.
/// Wraps <see cref="AeroDBUserStore{TUser, TRole, TKey}"/> with the Aero user/role types and a long key.
/// </summary>
/// <typeparam name="TUser">The user type, must inherit from <see cref="AeroUser"/>.</typeparam>
/// <typeparam name="TRole">The role type, must inherit from <see cref="AeroRole"/>.</typeparam>
public class UserStore<TUser, TRole> : AeroDBUserStore<TUser, TRole, long>
    where TUser : AeroUser, new()
    where TRole : AeroRole, new()
{
    /// <summary>
    /// Constructs a new instance of <see cref="UserStore{TUser, TRole}"/>.
    /// </summary>
    /// <param name="store">The AeroDB document store.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="identityOptions">ASP.NET Core Identity options.</param>
    /// <param name="describer">Identity error describer.</param>
    public UserStore(
        IDocumentStore store,
        ILogger<UserStore<TUser, TRole>> logger,
        IOptions<IdentityOptions>? identityOptions = null,
        IdentityErrorDescriber? describer = null)
        : base(store, (ILogger)logger, identityOptions, describer)
    {
    }
}
