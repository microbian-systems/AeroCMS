using System.Security.Claims;
using System.Globalization;
using Aero.Core.Identity;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Marten.Identity;

internal class RoleStore<TRole>(IDocumentSession session) :
    IQueryableRoleStore<TRole>,
    IRoleClaimStore<TRole>
    where TRole : AeroRole
{
        /// <summary>
    /// Dispose method.
    /// </summary>
public void Dispose()
    {
        session.Dispose();
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            session.Store(role);

            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {    
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            session.Update(role);
        
            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            session.Delete(role);

            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

        /// <summary>
    /// GetRoleIdAsync method.
    /// </summary>
public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateParameters(role, cancellationToken);

        return Task.FromResult(role.Id.ToString(CultureInfo.InvariantCulture));
    }

        /// <summary>
    /// GetRoleNameAsync method.
    /// </summary>
public Task<string> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateParameters(role, cancellationToken);

        return Task.FromResult(role.Name);
    }

        /// <summary>
    /// SetRoleNameAsync method.
    /// </summary>
public Task SetRoleNameAsync(TRole role, string roleName, CancellationToken cancellationToken)
    {
        if (roleName == null)
            throw new ArgumentNullException(nameof(roleName));

        ValidateParameters(role, cancellationToken);

        role.Name = roleName;

        return Task.CompletedTask;
    }

        /// <summary>
    /// GetNormalizedRoleNameAsync method.
    /// </summary>
public Task<string> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateParameters(role, cancellationToken);

        return Task.FromResult(role.NormalizedName);
    }

        /// <summary>
    /// SetNormalizedRoleNameAsync method.
    /// </summary>
public Task SetNormalizedRoleNameAsync(TRole role, string normalizedName, CancellationToken cancellationToken)
    {
        if (normalizedName == null)
            throw new ArgumentNullException(nameof(normalizedName));

        ValidateParameters(role, cancellationToken);

        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

        /// <summary>
    /// FindByIdAsync method.
    /// </summary>
public Task<TRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(roleId, out var parsedRoleId)) return Task.FromResult<TRole?>(null);
        return session.Query<TRole>().FirstOrDefaultAsync(x => x.Id == parsedRoleId, cancellationToken);
    }

        /// <summary>
    /// FindByNameAsync method.
    /// </summary>
public Task<TRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        return session.Query<TRole>()
            .FirstOrDefaultAsync(x => x.NormalizedName == normalizedRoleName, cancellationToken);
    }

        /// <summary>
    /// Gets or sets the Roles.
    /// </summary>
public IQueryable<TRole> Roles => session.Query<TRole>();

        /// <summary>
    /// GetClaimsAsync method.
    /// </summary>
public Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = new())
    {
        ValidateParameters(role, cancellationToken);

        var claims = role.Claims
            .Select(c => new Claim(c.ClaimType, c.ClaimValue))
            .ToList();

        return Task.FromResult<IList<Claim>>(claims);
    }

        /// <summary>
    /// AddClaimAsync method.
    /// </summary>
public Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = new())
    {
        ValidateParameters(role, cancellationToken);

        if (claim == null)
            throw new ArgumentNullException(nameof(claim));

        var roleClaim = new IdentityRoleClaim<long>
        {
            ClaimType = claim.Type,
            ClaimValue = claim.Value
        };
        role.Claims.Add(roleClaim);

        return Task.CompletedTask;
    }

        /// <summary>
    /// RemoveClaimAsync method.
    /// </summary>
public Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = new())
    {
        ValidateParameters(role, cancellationToken);
        IdentityRoleClaim<long> test;
        if (claim == null)
            throw new ArgumentNullException(nameof(claim));

        var matched = role.Claims
            .Where(u => u.ClaimValue == claim.Value && u.ClaimType == claim.Type)
            .ToList();

        foreach (var m in matched)
            role.Claims.Remove(m);

        return Task.CompletedTask;
    }

    private static void ValidateParameters(AeroRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (role == null)
            throw new ArgumentNullException(nameof(role));
    }
}
