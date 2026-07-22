using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

internal interface IManagerFederationLinkService
{
    Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteAsync(
        PreparedManagerFederationCallback prepared,
        ValidatedManagerIdentity identity,
        CancellationToken cancellationToken = default);

    Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteSignInAsync(
        PreparedManagerFederationCallback prepared,
        ValidatedManagerIdentity identity,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(long sessionId, long userId, CancellationToken cancellationToken = default);
}

internal sealed class ManagerFederationLinkService(
    IDocumentSession session,
    UserManager<AeroUser> userManager,
    IRecoveryAdministratorAuthority recoveryAdministratorAuthority,
    TimeProvider timeProvider) : IManagerFederationLinkService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public async Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteAsync(
        PreparedManagerFederationCallback prepared,
        ValidatedManagerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var binding = prepared.Binding;
        var state = prepared.State;
        var intent = prepared.LinkIntent;
        if (intent is null || state.ConsumedAt is not null || intent.ConsumedAt is not null ||
            state.Purpose != ManagerAuthenticationState.LinkRecoveryAdministratorPurpose ||
            binding.IsActive || binding.IsVerified ||
            !string.Equals(identity.Provider, binding.Provider, StringComparison.Ordinal) ||
            !string.Equals(identity.Issuer, binding.Issuer, StringComparison.Ordinal) ||
            !string.Equals(identity.OrganizationId, binding.OrganizationId, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityRules.IsOpaque(identity.Subject))
            return Fail();

        var authoritativeUserId = await recoveryAdministratorAuthority.GetUserIdAsync(cancellationToken);
        if (authoritativeUserId is not > 0 || authoritativeUserId != intent.RecoveryAdministratorUserId)
            return Fail();

        // Identity is deliberately touched only after callback state and provider identity validation.
        var user = await userManager.FindByIdAsync(authoritativeUserId.Value.ToString());
        if (user is null || !user.IsActive || user.IsDeleted ||
            !await userManager.IsInRoleAsync(user, CmsRoleNames.Admin) ||
            !await HasRecoveryClaimAsync(user))
            return Fail();

        var loginProvider = $"AeroCms.ManagerFederation.{binding.Provider}";
        var alreadyLinked = await userManager.FindByLoginAsync(loginProvider, identity.Subject);
        if (alreadyLinked is not null && alreadyLinked.Id != user.Id)
            return Prelude.Fail<ManagerFederationCallbackResult, AeroError>(
                AeroError.ConflictError("The manager provider identity is already linked."));

        var addedLogin = alreadyLinked is null;
        if (addedLogin)
        {
            var added = await userManager.AddLoginAsync(user,
                new UserLoginInfo(loginProvider, identity.Subject, binding.Provider));
            if (!added.Succeeded)
                return Prelude.Fail<ManagerFederationCallbackResult, AeroError>(
                    AeroError.ConflictError("The manager provider identity could not be linked."));
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            state.ConsumedAt = now;
            state.ModifiedOn = now;
            intent.ConsumedAt = now;
            intent.ModifiedOn = now;
            binding.IsVerified = true;
            binding.IsActive = true;
            binding.VerifiedByUserId = user.Id;
            binding.VerifiedAt = now;
            binding.ActivatedAtUtc = now;
            binding.ActivatedByRecoveryAdministratorUserId = user.Id;
            binding.ModifiedOn = now;

            var sessionRecord = new ManagerFederatedSession
            {
                Id = Snowflake.NewId(),
                UserId = user.Id,
                AuthorityBindingId = binding.Id,
                LoginProvider = loginProvider,
                ProviderKeyDigest = Digest(identity.Provider, identity.Issuer, identity.Subject),
                ProviderSessionReference = ManagerIdentityAuthorityRules.IsOpaque(identity.ProviderSessionReference)
                    ? identity.ProviderSessionReference
                    : null,
                ExpiresAt = now.Add(SessionLifetime),
                CreatedOn = now
            };

            session.Store(state);
            session.Store(intent);
            session.Store(binding);
            session.Store(sessionRecord);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ManagerFederationCallbackResult, AeroError>(
                new(user.Id, sessionRecord.Id, loginProvider, identity.Subject, binding.Provider,
                    sessionRecord.ExpiresAt, state.ReturnPath, true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            session.ClearChanges();
            if (addedLogin)
                await userManager.RemoveLoginAsync(user, loginProvider, identity.Subject);
            return Prelude.Fail<ManagerFederationCallbackResult, AeroError>(
                AeroError.DatabaseError("The manager provider link could not be completed."));
        }
    }

    public async Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteSignInAsync(
        PreparedManagerFederationCallback prepared,
        ValidatedManagerIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var binding = prepared.Binding;
        var state = prepared.State;
        if (prepared.LinkIntent is not null || state.ConsumedAt is not null ||
            state.Purpose != ManagerAuthenticationState.SignInPurpose ||
            !binding.IsActive || !binding.IsVerified ||
            !string.Equals(identity.Provider, binding.Provider, StringComparison.Ordinal) ||
            !string.Equals(identity.Issuer, binding.Issuer, StringComparison.Ordinal) ||
            !string.Equals(identity.OrganizationId, binding.OrganizationId, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityRules.IsOpaque(identity.Subject))
            return Fail();

        var loginProvider = $"AeroCms.ManagerFederation.{binding.Provider}";
        var user = await userManager.FindByLoginAsync(loginProvider, identity.Subject);
        if (user is null || !user.IsActive || user.IsDeleted ||
            !(await userManager.GetRolesAsync(user)).Intersect(CmsRoleNames.All, StringComparer.OrdinalIgnoreCase).Any())
            return Fail();

        try
        {
            var now = timeProvider.GetUtcNow();
            state.ConsumedAt = now;
            state.ModifiedOn = now;
            var sessionRecord = new ManagerFederatedSession
            {
                Id = Snowflake.NewId(), UserId = user.Id, AuthorityBindingId = binding.Id,
                LoginProvider = loginProvider,
                ProviderKeyDigest = Digest(identity.Provider, identity.Issuer, identity.Subject),
                ProviderSessionReference = ManagerIdentityAuthorityRules.IsOpaque(identity.ProviderSessionReference)
                    ? identity.ProviderSessionReference : null,
                ExpiresAt = now.Add(SessionLifetime), CreatedOn = now
            };
            session.Store(state);
            session.Store(sessionRecord);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ManagerFederationCallbackResult, AeroError>(new(
                user.Id, sessionRecord.Id, loginProvider, identity.Subject, binding.Provider,
                sessionRecord.ExpiresAt, state.ReturnPath, false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            session.ClearChanges();
            return Prelude.Fail<ManagerFederationCallbackResult, AeroError>(
                AeroError.DatabaseError("The manager provider sign-in could not be completed."));
        }
    }

    public async Task RevokeSessionAsync(long sessionId, long userId, CancellationToken cancellationToken = default)
    {
        if (sessionId <= 0 || userId <= 0) return;
        var record = await session.LoadAsync<ManagerFederatedSession>(sessionId, cancellationToken);
        if (record is null || record.UserId != userId || record.RevokedAt is not null) return;
        record.RevokedAt = timeProvider.GetUtcNow();
        record.ModifiedOn = record.RevokedAt;
        session.Store(record);
        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasRecoveryClaimAsync(AeroUser user)
    {
        var claims = await userManager.GetClaimsAsync(user);
        return claims.Any(claim =>
            string.Equals(claim.Type, ManagerRecoveryDefaults.AdministratorClaimType, StringComparison.Ordinal) &&
            string.Equals(claim.Value, ManagerRecoveryDefaults.AdministratorClaimValue, StringComparison.Ordinal));
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

    private static Result<ManagerFederationCallbackResult, AeroError> Fail() =>
        Prelude.Fail<ManagerFederationCallbackResult, AeroError>(
            AeroError.ValidationError(["Manager federation link is invalid."]));
}
