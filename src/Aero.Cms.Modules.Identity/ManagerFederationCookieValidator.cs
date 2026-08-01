using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

/// <summary>Revalidates federated manager cookies against durable local session and Identity state.</summary>
public sealed class ManagerFederationCookieValidator(
    IDocumentStore store,
    UserManager<AeroUser> userManager,
    TimeProvider timeProvider,
    IManagerAuthenticationModeResolver modeResolver)
{
    /// <summary>Leaves local manager cookies unchanged and rejects invalid federated manager cookies.</summary>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            await RejectAsync(context);
            return;
        }

        var sessionClaims = principal.FindAll(ManagerFederationClaims.SessionId).ToArray();
        var providerClaims = principal.FindAll(ManagerFederationClaims.Provider).ToArray();

        ManagerAuthenticationModeResolution mode;
        try
        {
            var resolved = await modeResolver.ResolveAsync(context.HttpContext.RequestAborted);
            if (resolved is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var value))
            {
                await RejectAsync(context);
                return;
            }
            mode = value;
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await RejectAsync(context);
            return;
        }

        // An unmarked local application cookie is valid only while local Identity is effective.
        if (sessionClaims.Length == 0 && providerClaims.Length == 0)
        {
            if (mode.Status == ManagerAuthenticationModeStatuses.Remote)
                await RejectAsync(context);
            return;
        }

        // Federation cookies are invalid during local and pending operation.
        if (mode.Status != ManagerAuthenticationModeStatuses.Remote)
        {
            await RejectAsync(context);
            return;
        }

        try
        {
            var userIdClaims = principal.FindAll(ClaimTypes.NameIdentifier).ToArray();
            if (sessionClaims.Length != 1 || providerClaims.Length != 1 || userIdClaims.Length != 1 ||
                !long.TryParse(sessionClaims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                    out var sessionId) || sessionId <= 0 ||
                !string.Equals(sessionClaims[0].Value,
                    sessionId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                !long.TryParse(userIdClaims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                    out var userId) || userId <= 0 ||
                !string.Equals(userIdClaims[0].Value,
                    userId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                !ManagerIdentityProviders.IsSupported(providerClaims[0].Value) ||
                !string.Equals(providerClaims[0].Value, mode.EffectiveProvider, StringComparison.Ordinal))
            {
                await RejectAsync(context);
                return;
            }

            var provider = providerClaims[0].Value;
            await using var query = await store.QuerySessionAsync(context.HttpContext.RequestAborted);
            var session = await query.LoadAsync<ManagerFederatedSession>(
                sessionId, context.HttpContext.RequestAborted);
            if (session is null || session.Id != sessionId || session.UserId != userId ||
                session.AuthorityBindingId <= 0 || session.RevokedAt is not null ||
                session.ExpiresAt <= timeProvider.GetUtcNow() ||
                !string.Equals(session.LoginProvider,
                    $"AeroCms.ManagerFederation.{provider}", StringComparison.Ordinal))
            {
                await RejectAsync(context);
                return;
            }

            var binding = await query.LoadAsync<ManagerIdentityAuthorityBinding>(
                session.AuthorityBindingId, context.HttpContext.RequestAborted);
            if (binding is null || binding.Id != session.AuthorityBindingId ||
                binding.Id != mode.AuthorityBindingId ||
                !binding.IsActive || !binding.IsVerified ||
                !string.Equals(binding.SingletonKey,
                    ManagerIdentityAuthorityBinding.InstallationSingletonKey, StringComparison.Ordinal) ||
                !string.Equals(binding.Provider, provider, StringComparison.Ordinal) ||
                !ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: true, out _))
            {
                await RejectAsync(context);
                return;
            }

            var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture));
            if (user is null || user.Id != userId || !user.IsActive || user.IsDeleted ||
                !(await userManager.GetRolesAsync(user)).Intersect(
                    CmsRoleNames.All, StringComparer.OrdinalIgnoreCase).Any())
            {
                await RejectAsync(context);
                return;
            }

            var matchingLogins = (await userManager.GetLoginsAsync(user))
                .Where(login => string.Equals(login.LoginProvider, session.LoginProvider, StringComparison.Ordinal))
                .ToArray();
            if (matchingLogins.Length != 1 ||
                !string.Equals(session.ProviderKeyDigest,
                    Digest(provider, binding.Issuer, matchingLogins[0].ProviderKey), StringComparison.Ordinal))
            {
                await RejectAsync(context);
                return;
            }

            var owningUser = await userManager.FindByLoginAsync(
                matchingLogins[0].LoginProvider, matchingLogins[0].ProviderKey);
            if (owningUser is null || owningUser.Id != userId)
                await RejectAsync(context);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await RejectAsync(context);
        }
    }

    private static string Digest(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in values)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
            hash.AppendData([0]);
        }

        return WebEncoders.Base64UrlEncode(hash.GetHashAndReset());
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
