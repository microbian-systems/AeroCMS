using System.Security.Claims;
using System.Globalization;
using Aero.Core.Identity;
using Aero.Models.Entities;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Marten.Identity;

internal class UserStore<TUser>(IDocumentSession session) :
    IUserLoginStore<TUser>,
    IUserClaimStore<TUser>,
    IUserPasswordStore<TUser>,
    IUserSecurityStampStore<TUser>,
    IUserEmailStore<TUser>,
    IUserLockoutStore<TUser>,
    IUserPhoneNumberStore<TUser>,
    IQueryableUserStore<TUser>,
    IUserTwoFactorStore<TUser>,
    IUserAuthenticationTokenStore<TUser>,
    IUserAuthenticatorKeyStore<TUser>,
    IUserTwoFactorRecoveryCodeStore<TUser>,
    IUserRoleStore<TUser>
    where TUser : AeroUser
{
    private const string InternalLoginProvider = "InternalProvider";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
    private const string RecoveryCodeTokenName = "RecoveryCodes";
    private readonly IDocumentSession _session = session;

        /// <summary>
    /// Gets or sets the Users.
    /// </summary>
public IQueryable<TUser> Users => _session.Query<TUser>();

        /// <summary>
    /// SetTokenAsync method.
    /// </summary>
public Task SetTokenAsync(TUser user, string loginProvider, string name, string value,
        CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var token = user.Tokens
            .FirstOrDefault(t => t.LoginProvider == loginProvider && t.Name == name);
        
        if (token == null)
        {
            token = new IdentityToken
            {
                LoginProvider = loginProvider,
                Name = name
            };
            user.Tokens.Add(token);
        }

        token.Value = value;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// RemoveTokenAsync method.
    /// </summary>
public Task RemoveTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var matched = user.Tokens
            .Where(t => t.LoginProvider == loginProvider && t.Name == name)
            .ToList();

        foreach (var m in matched)
            user.Tokens.Remove(m);
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetTokenAsync method.
    /// </summary>
public Task<string> GetTokenAsync(TUser user, string loginProvider, string name,
        CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var token = user.Tokens
            .Where(t => t.LoginProvider == loginProvider && t.Name == name)
            .Select(t => t.Value)
            .FirstOrDefault();

        return Task.FromResult(token)!;
    }

        /// <summary>
    /// SetAuthenticatorKeyAsync method.
    /// </summary>
public Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken)
    {
        return SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, key, cancellationToken);
    }

        /// <summary>
    /// GetAuthenticatorKeyAsync method.
    /// </summary>
public Task<string> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken)
    {
        return GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);
    }

        /// <summary>
    /// GetClaimsAsync method.
    /// </summary>
public Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var claims = user.Claims
            .Select(c => new Claim(c.ClaimType, c.ClaimValue))
            .ToList();

        return Task.FromResult<IList<Claim>>(claims);
    }

        /// <summary>
    /// AddClaimsAsync method.
    /// </summary>
public Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        if (claims == null)
            throw new ArgumentNullException(nameof(claims));

        foreach (var claim in claims)
        {
            var userClaim = new IdentityUserClaim<long>
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value
            };
            user.Claims.Add(userClaim);
        }
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// ReplaceClaimAsync method.
    /// </summary>
public Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        if (claim == null)
            throw new ArgumentNullException(nameof(claim));

        if (newClaim == null)
            throw new ArgumentNullException(nameof(newClaim));

        var matched = user.Claims
            .Where(uc => uc.ClaimValue == claim.Value && uc.ClaimType == claim.Type);

        foreach (var m in matched)
        {
            m.ClaimValue = newClaim.Value;
            m.ClaimType = newClaim.Type;
        }

        return Task.CompletedTask;
    }

        /// <summary>
    /// RemoveClaimsAsync method.
    /// </summary>
public Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        if (claims == null)
            throw new ArgumentNullException(nameof(claims));

        foreach (var claim in claims)
        {
            var matched = user.Claims
                .Where(u => u.ClaimValue == claim.Value && u.ClaimType == claim.Type)
                .ToList();

            foreach (var m in matched)
                user.Claims.Remove(m);
        }

        return Task.CompletedTask;
    }

        /// <summary>
    /// GetUsersForClaimAsync method.
    /// </summary>
public async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        if (claim == null)
            throw new ArgumentNullException(nameof(claim));

        cancellationToken.ThrowIfCancellationRequested();

        return (await _session.Query<TUser>().Where(u => u.Claims.Any(c => c.ClaimType == claim.Type && c.ClaimValue == claim.Value))
            .ToListAsync(cancellationToken)).ToList();
    }

        /// <summary>
    /// SetEmailAsync method.
    /// </summary>
public Task SetEmailAsync(TUser user, string email, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.Email = email;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetEmailAsync method.
    /// </summary>
public Task<string> GetEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.Email)!;
    }

        /// <summary>
    /// GetEmailConfirmedAsync method.
    /// </summary>
public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.EmailConfirmed);
    }

        /// <summary>
    /// SetEmailConfirmedAsync method.
    /// </summary>
public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.EmailConfirmed = confirmed;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// FindByEmailAsync method.
    /// </summary>
public async Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await _session.Query<TUser>().FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    }

        /// <summary>
    /// GetNormalizedEmailAsync method.
    /// </summary>
public Task<string> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.NormalizedEmail)!;
    }

        /// <summary>
    /// SetNormalizedEmailAsync method.
    /// </summary>
public Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetLockoutEndDateAsync method.
    /// </summary>
public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.LockoutEnd);
    }

        /// <summary>
    /// SetLockoutEndDateAsync method.
    /// </summary>
public Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

        /// <summary>
    /// IncrementAccessFailedCountAsync method.
    /// </summary>
public Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.AccessFailedCount++;
        
        return Task.FromResult(user.AccessFailedCount);
    }

        /// <summary>
    /// ResetAccessFailedCountAsync method.
    /// </summary>
public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetAccessFailedCountAsync method.
    /// </summary>
public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.AccessFailedCount);
    }

        /// <summary>
    /// GetLockoutEnabledAsync method.
    /// </summary>
public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.LockoutEnabled);
    }

        /// <summary>
    /// SetLockoutEnabledAsync method.
    /// </summary>
public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

        /// <summary>
    /// Dispose method.
    /// </summary>
public void Dispose()
    {
        _session.Dispose();
    }

        /// <summary>
    /// GetUserIdAsync method.
    /// </summary>
public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.Id.ToString(CultureInfo.InvariantCulture))!;
    }

        /// <summary>
    /// GetUserNameAsync method.
    /// </summary>
public Task<string> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.UserName)!;
    }

        /// <summary>
    /// SetUserNameAsync method.
    /// </summary>
public Task SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.UserName = userName;
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetNormalizedUserNameAsync method.
    /// </summary>
public Task<string> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.NormalizedUserName)!;
    }

        /// <summary>
    /// SetNormalizedUserNameAsync method.
    /// </summary>
public Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken)
    {
        if (normalizedName == null)
            throw new ArgumentNullException(nameof(normalizedName));

        ValidateParameters(user, cancellationToken);

        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

        /// <summary>
    /// CreateAsync method.
    /// </summary>
public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            _session.Store(user);
            
            await _session.SaveChangesAsync(cancellationToken);

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
public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            _session.Update(user);
            
            await _session.SaveChangesAsync(cancellationToken);

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
public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            _session.Delete(user);
            
            await _session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {    
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

        /// <summary>
    /// FindByIdAsync method.
    /// </summary>
public Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(userId, out var id)) return Task.FromResult<TUser?>(null);
        return FindByIdAsync(id, cancellationToken);
    }

        /// <summary>
    /// FindByIdAsync method.
    /// </summary>
public Task<TUser?> FindByIdAsync(long userId, CancellationToken cancellationToken)
    {
        return _session.Query<TUser>().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

        /// <summary>
    /// FindByNameAsync method.
    /// </summary>
public Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return _session.Query<TUser>()
            .FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken);
    }

        /// <summary>
    /// AddLoginAsync method.
    /// </summary>
public Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        if (login == null)
            throw new ArgumentNullException(nameof(login));

        var userLogin = new IdentityLogin
        {
            LoginProvider = login.LoginProvider,
            ProviderKey = login.ProviderKey,
            ProviderDisplayName = login.ProviderDisplayName
        };

        user.Logins.Add(userLogin);

        return Task.CompletedTask;
    }

        /// <summary>
    /// RemoveLoginAsync method.
    /// </summary>
public Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var matchedLogins = user.Logins
            .Where(u => u.LoginProvider == loginProvider && u.ProviderKey == providerKey)
            .ToList();

        foreach (var matchedLogin in matchedLogins)
            user.Logins.Remove(matchedLogin);
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetLoginsAsync method.
    /// </summary>
public Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult<IList<UserLoginInfo>>(user.Logins
            .Select(u => new UserLoginInfo(u.LoginProvider, u.ProviderKey, u.ProviderDisplayName))
            .ToList());
    }

        /// <summary>
    /// FindByLoginAsync method.
    /// </summary>
public Task<TUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _session.Query<TUser>().FirstOrDefaultAsync(u =>
            u.Logins.Any(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey), cancellationToken);
    }

        /// <summary>
    /// SetPasswordHashAsync method.
    /// </summary>
public Task SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.PasswordHash = passwordHash;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetPasswordHashAsync method.
    /// </summary>
public Task<string> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.PasswordHash)!;
    }

        /// <summary>
    /// HasPasswordAsync method.
    /// </summary>
public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
    }

        /// <summary>
    /// SetPhoneNumberAsync method.
    /// </summary>
public Task SetPhoneNumberAsync(TUser user, string phoneNumber, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.PhoneNumber = phoneNumber;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetPhoneNumberAsync method.
    /// </summary>
public Task<string> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.PhoneNumber)!;
    }

        /// <summary>
    /// GetPhoneNumberConfirmedAsync method.
    /// </summary>
public Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.PhoneNumberConfirmed);
    }

        /// <summary>
    /// SetPhoneNumberConfirmedAsync method.
    /// </summary>
public Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.PhoneNumberConfirmed = confirmed;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// AddToRoleAsync method.
    /// </summary>
public Task AddToRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        if (user.Roles.All(r => r.Name != roleName))
        {
            user.Roles.Add(new AeroRole(roleName));
        }

        return Task.CompletedTask;
    }

        /// <summary>
    /// RemoveFromRoleAsync method.
    /// </summary>
public async Task RemoveFromRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var role = user.Roles.FirstOrDefault(r => r.Name == roleName);
        if (role != null)
        {
            user.Roles.Remove(role);
        }
    }

        /// <summary>
    /// GetRolesAsync method.
    /// </summary>
public Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult<IList<string>>(user.Roles.Select(r => r.Name!).ToList());
    }

        /// <summary>
    /// IsInRoleAsync method.
    /// </summary>
public Task<bool> IsInRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var isInRole = user.Roles.Any(r => r.Name == roleName);
        return Task.FromResult(isInRole);
    }

        /// <summary>
    /// GetUsersInRoleAsync method.
    /// </summary>
public async Task<IList<TUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        return (await _session.Query<TUser>().Where(u => u.Roles.Any(r => r.Name == roleName)).ToListAsync(cancellationToken)).ToList();
    }

        /// <summary>
    /// SetSecurityStampAsync method.
    /// </summary>
public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.SecurityStamp = stamp;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetSecurityStampAsync method.
    /// </summary>
public Task<string> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.SecurityStamp)!;
    }

        /// <summary>
    /// ReplaceCodesAsync method.
    /// </summary>
public Task ReplaceCodesAsync(TUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        if (recoveryCodes == null)
            throw new ArgumentNullException(nameof(recoveryCodes));

        var mergedCodes = string.Join(";", recoveryCodes);
        return SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, mergedCodes, cancellationToken);
    }

        /// <summary>
    /// RedeemCodeAsync method.
    /// </summary>
public async Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ??
                          "";

        var splitCodes = mergedCodes.Split(';');
        if (splitCodes.Contains(code))
        {
            var updatedCodes = splitCodes
                .Where(s => s != code)
                .ToList();

            await ReplaceCodesAsync(user, updatedCodes, cancellationToken);

            return true;
        }

        return false;
    }

        /// <summary>
    /// CountCodesAsync method.
    /// </summary>
public async Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ??
                          "";
        if (mergedCodes.Length <= 0)
            return 0;

        return mergedCodes.Split(';').Length;
    }

        /// <summary>
    /// SetTwoFactorEnabledAsync method.
    /// </summary>
public Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        user.TwoFactorEnabled = enabled;
        
        return Task.CompletedTask;
    }

        /// <summary>
    /// GetTwoFactorEnabledAsync method.
    /// </summary>
public Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateParameters(user, cancellationToken);

        return Task.FromResult(user.TwoFactorEnabled);
    }

    private static void ValidateParameters(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (user == null)
            throw new ArgumentNullException(nameof(user));
    }
}
