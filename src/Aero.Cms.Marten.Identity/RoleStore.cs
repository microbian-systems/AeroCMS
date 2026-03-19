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
    public void Dispose()
    {
        session.Dispose();
    }

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

    public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateParameters(role, cancellationToken);

        return Task.FromResult(role.Id.ToString(CultureInfo.InvariantCulture));
    }

    public Task<string> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateParameters(role, cancellationToken);

        return Task.FromResult(role.Name);
    }

    public Task SetRoleNameAsync(TRole role, string roleName, CancellationToken cancellationToken)
    {
        if (roleName == null)
            throw new ArgumentNullException(nameof(roleName));

        ValidateParameters(role, cancellationToken);

        role.Name = roleName;

        return Task.CompletedTask;
    }

    public Task<string> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateParameters(role, cancellationToken);

        return Task.FromResult(role.NormalizedName);
    }

    public Task SetNormalizedRoleNameAsync(TRole role, string normalizedName, CancellationToken cancellationToken)
    {
        if (normalizedName == null)
            throw new ArgumentNullException(nameof(normalizedName));

        ValidateParameters(role, cancellationToken);

        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public Task<TRole> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        var parsedRoleId = ulong.Parse(roleId, CultureInfo.InvariantCulture);
        return session.Query<TRole>().FirstAsync(x => x.Id == parsedRoleId, cancellationToken);
    }

    public Task<TRole> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        return session.Query<TRole>()
            .FirstAsync(x => x.NormalizedName == normalizedRoleName, cancellationToken);
    }

    public IQueryable<TRole> Roles => session.Query<TRole>();

    public Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = new())
    {
        ValidateParameters(role, cancellationToken);

        var claims = role.Claims
            .Select(c => new Claim(c.ClaimType, c.ClaimValue))
            .ToList();

        return Task.FromResult<IList<Claim>>(claims);
    }

    public Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = new())
    {
        ValidateParameters(role, cancellationToken);

        if (claim == null)
            throw new ArgumentNullException(nameof(claim));

        var roleClaim = new IdentityRoleClaim<ulong>
        {
            ClaimType = claim.Type,
            ClaimValue = claim.Value
        };
        role.Claims.Add(roleClaim);

        return Task.CompletedTask;
    }

    public Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = new())
    {
        ValidateParameters(role, cancellationToken);
        IdentityRoleClaim<ulong> test;
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
