using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

/// <summary>Creates invitation-gated, tenant-scoped local storefront credentials.</summary>
public sealed class LocalExternalMemberAuthenticationService(
    IDocumentSession session,
    IValidator<CreateLocalExternalMemberInvitationRequest> invitationValidator,
    IValidator<ActivateLocalExternalMemberInvitationRequest> activationValidator,
    IValidator<LoginLocalExternalMemberRequest> loginValidator,
    IValidator<ResetLocalExternalMemberPasswordRequest> resetValidator,
    IValidator<IssueLocalExternalMemberPasswordResetRequest> issueResetValidator,
    IPasswordHasher<ExternalMemberLocalCredential> passwordHasher,
    LocalExternalMemberPasswordSentinel passwordSentinel,
    TimeProvider timeProvider) : ILocalExternalMemberAuthenticationService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan LockoutLifetime = TimeSpan.FromMinutes(15);
    private const int LockoutThreshold = 5;

    public async Task<Result<LocalExternalMemberPasswordResetHandle, AeroError>> IssuePasswordResetAsync(
        IssueLocalExternalMemberPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await issueResetValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return ValidationFailure<LocalExternalMemberPasswordResetHandle>(validation.Errors.Select(x => x.ErrorMessage));

            var now = timeProvider.GetUtcNow();
            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            var member = await session.LoadAsync<ExternalMember>(request.ExternalMemberId, cancellationToken);
            var authorities = await session.Query<ExternalMemberLocalAuthority>()
                .Where(value => value.TenantId == request.TenantId && value.IsActive)
                .ToListAsync(cancellationToken);
            var credentials = await session.Query<ExternalMemberLocalCredential>()
                .Where(value => value.TenantId == request.TenantId &&
                    value.ExternalMemberId == request.ExternalMemberId && value.IsActive)
                .ToListAsync(cancellationToken);
            var assignments = await session.Query<ExternalMemberSiteAssignment>()
                .Where(value => value.ExternalMemberId == request.ExternalMemberId &&
                    value.TenantId == request.TenantId && value.SiteId == request.SiteId && value.IsActive)
                .ToListAsync(cancellationToken);
            if (site is not { IsEnabled: true } || site.TenantId != request.TenantId ||
                member is not { IsActive: true } || authorities.Count != 1 || credentials.Count != 1 ||
                assignments.Count != 1 || credentials[0].SecurityVersion != member.SecurityVersion)
                return Fail<LocalExternalMemberPasswordResetHandle>("Local password reset is unavailable.");

            var credential = credentials[0];
            var issuer = LocalIssuer(authorities[0].Id);
            var subject = credential.Id.ToString(CultureInfo.InvariantCulture);
            var links = await session.Query<ExternalIdentityLink>()
                .Where(value => value.ExternalMemberId == member.Id && value.IsActive &&
                    value.Provider == LocalExternalMemberAuthentication.Provider && value.Issuer == issuer &&
                    value.Subject == subject)
                .ToListAsync(cancellationToken);
            if (links.Count != 1)
                return Fail<LocalExternalMemberPasswordResetHandle>("Local password reset is unavailable.");

            var outstanding = await session.Query<ExternalMemberPasswordReset>()
                .Where(value => value.TenantId == request.TenantId && value.CredentialId == credential.Id &&
                    value.ConsumedAt == null && value.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var prior in outstanding)
            {
                prior.RevokedAt = now;
                prior.ModifiedOn = now;
                session.Store(prior);
            }

            var secret = CreateSecret();
            var reset = new ExternalMemberPasswordReset
            {
                Id = Snowflake.NewId(), TenantId = request.TenantId, CredentialId = credential.Id,
                TokenDigest = DigestSecret(secret), CapturedCredentialSecurityVersion = credential.SecurityVersion,
                ExpiresAt = request.ExpiresAt, IssuedByManagerUserId = request.IssuedByManagerUserId,
                CreatedOn = now
            };
            session.Store(reset);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<LocalExternalMemberPasswordResetHandle, AeroError>(
                new(CreateHandle(reset.Id, secret), reset.ExpiresAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { session.ClearChanges(); return Cancelled<LocalExternalMemberPasswordResetHandle>(); }
        catch (ConcurrencyException)
        { session.ClearChanges(); return Conflict<LocalExternalMemberPasswordResetHandle>(); }
        catch (Exception exception) when (IsUniqueConflict(exception))
        { session.ClearChanges(); return Conflict<LocalExternalMemberPasswordResetHandle>(); }
        catch
        { session.ClearChanges(); return DatabaseFailure<LocalExternalMemberPasswordResetHandle>("Local password reset could not be issued."); }
    }

    public async Task<Result<ExternalMemberInvitationHandle, AeroError>> CreateInvitationAsync(
        CreateLocalExternalMemberInvitationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await invitationValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return ValidationFailure<ExternalMemberInvitationHandle>(validation.Errors.Select(x => x.ErrorMessage));

            var authority = await session.LoadAsync<ExternalMemberLocalAuthority>(request.LocalAuthorityId, cancellationToken);
            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            if (authority is not { IsActive: true } || authority.TenantId != request.TenantId ||
                site is not { IsEnabled: true } || site.TenantId != request.TenantId)
                return Fail<ExternalMemberInvitationHandle>("Invitation scope is unavailable.");

            var secret = CreateSecret();
            var invitation = new ExternalMemberInvitation
            {
                Id = Snowflake.NewId(), TenantId = request.TenantId, SiteId = request.SiteId,
                LocalAuthorityId = authority.Id, OrganizationBindingId = null,
                Provider = LocalExternalMemberAuthentication.Provider,
                NormalizedEmail = NormalizeEmail(request.Email), TokenDigest = DigestSecret(secret),
                ExpiresAt = request.ExpiresAt, CreatedOn = timeProvider.GetUtcNow()
            };
            session.Store(invitation);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ExternalMemberInvitationHandle, AeroError>(new(CreateHandle(invitation.Id, secret), invitation.ExpiresAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { session.ClearChanges(); return Cancelled<ExternalMemberInvitationHandle>(); }
        catch (ConcurrencyException) { session.ClearChanges(); return Conflict<ExternalMemberInvitationHandle>(); }
        catch (Exception exception) when (IsUniqueConflict(exception)) { session.ClearChanges(); return Conflict<ExternalMemberInvitationHandle>(); }
        catch { session.ClearChanges(); return DatabaseFailure<ExternalMemberInvitationHandle>("Invitation could not be created."); }
    }

    public async Task<Result<ExternalMemberIssuanceReceipt, AeroError>> ActivateInvitationAsync(
        ActivateLocalExternalMemberInvitationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await activationValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return ValidationFailure<ExternalMemberIssuanceReceipt>(validation.Errors.Select(x => x.ErrorMessage));
            if (!TryParseHandle(request.InvitationHandle, out var invitationId, out var secret))
                return Fail<ExternalMemberIssuanceReceipt>("Local membership could not be activated.");

            var now = timeProvider.GetUtcNow();
            var invitation = await session.LoadAsync<ExternalMemberInvitation>(invitationId, cancellationToken);
            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            var authority = invitation?.LocalAuthorityId is > 0
                ? await session.LoadAsync<ExternalMemberLocalAuthority>(invitation.LocalAuthorityId.Value, cancellationToken)
                : null;
            var normalizedEmail = NormalizeEmail(request.Email);
            if (invitation is null || invitation.TenantId != request.TenantId || invitation.SiteId != request.SiteId ||
                invitation.OrganizationBindingId is not null || invitation.LocalAuthorityId is null ||
                !string.Equals(invitation.Provider, LocalExternalMemberAuthentication.Provider, StringComparison.Ordinal) ||
                invitation.ConsumedAt is not null || invitation.RevokedAt is not null || invitation.ExpiresAt <= now ||
                !string.Equals(invitation.NormalizedEmail, normalizedEmail, StringComparison.Ordinal) ||
                !VerifySecret(secret, invitation.TokenDigest) || authority is not { IsActive: true } ||
                authority.TenantId != request.TenantId || site is not { IsEnabled: true } || site.TenantId != request.TenantId)
                return Fail<ExternalMemberIssuanceReceipt>("Local membership could not be activated.");

            var existingCredential = await session.Query<ExternalMemberLocalCredential>()
                .FirstOrDefaultAsync(value => value.TenantId == request.TenantId && value.NormalizedEmail == normalizedEmail, cancellationToken);
            if (existingCredential is not null)
                return Conflict<ExternalMemberIssuanceReceipt>();

            var member = new ExternalMember
            {
                Id = Snowflake.NewId(), IsActive = true, SecurityVersion = 1,
                Email = request.Email.Trim().Normalize(NormalizationForm.FormKC),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(), CreatedOn = now
            };
            var credential = new ExternalMemberLocalCredential
            {
                Id = Snowflake.NewId(), TenantId = request.TenantId, ExternalMemberId = member.Id,
                NormalizedEmail = normalizedEmail, SecurityVersion = member.SecurityVersion, IsActive = true, CreatedOn = now
            };
            credential.PasswordHash = passwordHasher.HashPassword(credential, request.Password);
            var issuer = string.Create(CultureInfo.InvariantCulture, $"urn:aerocms:external-member-local-authority:{authority.Id}");
            var subject = credential.Id.ToString(CultureInfo.InvariantCulture);
            var link = new ExternalIdentityLink
            {
                Id = Snowflake.NewId(), Provider = LocalExternalMemberAuthentication.Provider, Issuer = issuer,
                Subject = subject, IdentityKey = ExternalMemberIssuanceService.ComputeKey(LocalExternalMemberAuthentication.Provider, issuer, subject),
                ExternalMemberId = member.Id, ExternalMemberInvitationId = invitation.Id, IsActive = true, CreatedOn = now
            };
            var assignment = new ExternalMemberSiteAssignment
            {
                Id = Snowflake.NewId(), ExternalMemberId = member.Id, TenantId = request.TenantId,
                SiteId = request.SiteId, IsActive = true, CreatedOn = now
            };
            var localSession = new ExternalMemberSession
            {
                Id = Snowflake.NewId(), TenantId = request.TenantId, SiteId = request.SiteId,
                ExternalMemberId = member.Id, ExternalIdentityLinkId = link.Id,
                AuthenticationProvider = LocalExternalMemberAuthentication.Provider, SecurityVersion = member.SecurityVersion,
                ExpiresAt = now.Add(SessionLifetime), CreatedOn = now
            };
            invitation.ConsumedAt = now;
            invitation.ConsumedByExternalMemberId = member.Id;
            invitation.ModifiedOn = now;
            session.Store(member); session.Store(credential); session.Store(link); session.Store(assignment); session.Store(localSession); session.Store(invitation);
            await session.SaveChangesAsync(cancellationToken);

            return Prelude.Ok<ExternalMemberIssuanceReceipt, AeroError>(new(member.Id, link.Id, localSession.Id,
                request.TenantId, request.SiteId, LocalExternalMemberAuthentication.Provider, member.SecurityVersion,
                localSession.ExpiresAt, request.ReturnPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { session.ClearChanges(); return Cancelled<ExternalMemberIssuanceReceipt>(); }
        catch (ConcurrencyException) { session.ClearChanges(); return Conflict<ExternalMemberIssuanceReceipt>(); }
        catch (Exception exception) when (IsUniqueConflict(exception)) { session.ClearChanges(); return Conflict<ExternalMemberIssuanceReceipt>(); }
        catch { session.ClearChanges(); return DatabaseFailure<ExternalMemberIssuanceReceipt>("Local membership could not be activated."); }
    }

    public async Task<Result<ExternalMemberIssuanceReceipt, AeroError>> LoginAsync(
        LoginLocalExternalMemberRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await loginValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return AuthenticationFailure<ExternalMemberIssuanceReceipt>();

            var now = timeProvider.GetUtcNow();
            var normalizedEmail = NormalizeEmail(request.Email);
            var credentials = await session.Query<ExternalMemberLocalCredential>()
                .Where(value => value.TenantId == request.TenantId && value.NormalizedEmail == normalizedEmail)
                .ToListAsync(cancellationToken);
            var credential = credentials.Count == 1 ? credentials[0] : null;

            PasswordVerificationResult verification;
            try
            {
                verification = passwordHasher.VerifyHashedPassword(
                    credential ?? passwordSentinel.Credential,
                    credential?.PasswordHash ?? passwordSentinel.PasswordHash,
                    request.Password);
            }
            catch
            {
                verification = PasswordVerificationResult.Failed;
            }

            if (credential is null)
                return AuthenticationFailure<ExternalMemberIssuanceReceipt>();

            var lockoutActive = credential.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now;
            if (!lockoutActive && credential.LockoutEndUtc is not null)
            {
                credential.LockoutEndUtc = null;
                credential.FailedAccessCount = 0;
                credential.ModifiedOn = now;
            }

            if (verification == PasswordVerificationResult.Failed)
            {
                if (!lockoutActive && credential.IsActive)
                {
                    credential.FailedAccessCount = checked(credential.FailedAccessCount + 1);
                    if (credential.FailedAccessCount >= LockoutThreshold)
                        credential.LockoutEndUtc = now.Add(LockoutLifetime);
                    credential.ModifiedOn = now;
                    session.Store(credential);
                    await session.SaveChangesAsync(cancellationToken);
                }

                return AuthenticationFailure<ExternalMemberIssuanceReceipt>();
            }

            if (lockoutActive || !credential.IsActive || credential.SecurityVersion <= 0)
                return AuthenticationFailure<ExternalMemberIssuanceReceipt>();

            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            var authorities = await session.Query<ExternalMemberLocalAuthority>()
                .Where(value => value.TenantId == request.TenantId && value.IsActive)
                .ToListAsync(cancellationToken);
            var member = await session.LoadAsync<ExternalMember>(credential.ExternalMemberId, cancellationToken);
            var assignments = await session.Query<ExternalMemberSiteAssignment>()
                .Where(value => value.ExternalMemberId == credential.ExternalMemberId &&
                    value.TenantId == request.TenantId && value.SiteId == request.SiteId && value.IsActive)
                .ToListAsync(cancellationToken);
            var issuer = authorities.Count == 1 ? LocalIssuer(authorities[0].Id) : string.Empty;
            var subject = credential.Id.ToString(CultureInfo.InvariantCulture);
            var links = await session.Query<ExternalIdentityLink>()
                .Where(value => value.ExternalMemberId == credential.ExternalMemberId &&
                    value.Provider == LocalExternalMemberAuthentication.Provider && value.Issuer == issuer &&
                    value.Subject == subject && value.IsActive)
                .ToListAsync(cancellationToken);

            if (site is not { IsEnabled: true } || site.TenantId != request.TenantId ||
                authorities.Count != 1 || member is not { IsActive: true } ||
                member.SecurityVersion != credential.SecurityVersion || assignments.Count != 1 || links.Count != 1)
            {
                return AuthenticationFailure<ExternalMemberIssuanceReceipt>();
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                credential.PasswordHash = passwordHasher.HashPassword(credential, request.Password);
            credential.FailedAccessCount = 0;
            credential.LockoutEndUtc = null;
            credential.ModifiedOn = now;

            var localSession = CreateSession(request.TenantId, request.SiteId, member, links[0], now);
            session.Store(credential);
            session.Store(localSession);
            await session.SaveChangesAsync(cancellationToken);

            return Prelude.Ok<ExternalMemberIssuanceReceipt, AeroError>(new(
                member.Id, links[0].Id, localSession.Id, request.TenantId, request.SiteId,
                LocalExternalMemberAuthentication.Provider, member.SecurityVersion, localSession.ExpiresAt,
                request.ReturnPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.ClearChanges();
            return Cancelled<ExternalMemberIssuanceReceipt>();
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return AuthenticationFailure<ExternalMemberIssuanceReceipt>();
        }
        catch
        {
            session.ClearChanges();
            return DatabaseFailure<ExternalMemberIssuanceReceipt>("Local sign-in could not be completed.");
        }
    }

    public async Task<Result<LocalExternalMemberPasswordResetReceipt, AeroError>> ResetPasswordAsync(
        ResetLocalExternalMemberPasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await resetValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid || !TryParseHandle(request.ResetHandle, out var resetId, out var secret))
                return AuthenticationFailure<LocalExternalMemberPasswordResetReceipt>();

            var now = timeProvider.GetUtcNow();
            var reset = await session.LoadAsync<ExternalMemberPasswordReset>(resetId, cancellationToken);
            var credential = reset is null
                ? null
                : await session.LoadAsync<ExternalMemberLocalCredential>(reset.CredentialId, cancellationToken);
            var member = credential is null
                ? null
                : await session.LoadAsync<ExternalMember>(credential.ExternalMemberId, cancellationToken);
            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            var authorities = await session.Query<ExternalMemberLocalAuthority>()
                .Where(value => value.TenantId == request.TenantId && value.IsActive)
                .ToListAsync(cancellationToken);
            var assignments = credential is null
                ? []
                : await session.Query<ExternalMemberSiteAssignment>()
                    .Where(value => value.ExternalMemberId == credential.ExternalMemberId &&
                        value.TenantId == request.TenantId && value.SiteId == request.SiteId && value.IsActive)
                    .ToListAsync(cancellationToken);
            var issuer = authorities.Count == 1 ? LocalIssuer(authorities[0].Id) : string.Empty;
            var subject = credential?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            var links = credential is null
                ? []
                : await session.Query<ExternalIdentityLink>()
                    .Where(value => value.ExternalMemberId == credential.ExternalMemberId &&
                        value.Provider == LocalExternalMemberAuthentication.Provider && value.Issuer == issuer &&
                        value.Subject == subject && value.IsActive)
                    .ToListAsync(cancellationToken);

            if (reset is null || credential is not { IsActive: true } || member is not { IsActive: true } ||
                site is not { IsEnabled: true } || site.TenantId != request.TenantId || authorities.Count != 1 ||
                reset.TenantId != request.TenantId || reset.CredentialId != credential.Id ||
                reset.CapturedCredentialSecurityVersion != credential.SecurityVersion ||
                credential.SecurityVersion != member.SecurityVersion || reset.ExpiresAt <= now ||
                reset.ConsumedAt is not null || reset.RevokedAt is not null ||
                !VerifySecret(secret, reset.TokenDigest) || assignments.Count != 1 || links.Count != 1)
            {
                return AuthenticationFailure<LocalExternalMemberPasswordResetReceipt>();
            }

            var newSecurityVersion = checked(Math.Max(credential.SecurityVersion, member.SecurityVersion) + 1);
            credential.PasswordHash = passwordHasher.HashPassword(credential, request.NewPassword);
            credential.SecurityVersion = newSecurityVersion;
            credential.FailedAccessCount = 0;
            credential.LockoutEndUtc = null;
            credential.ModifiedOn = now;
            member.SecurityVersion = newSecurityVersion;
            member.ModifiedOn = now;
            reset.ConsumedAt = now;
            reset.ModifiedOn = now;

            var activeSessions = await session.Query<ExternalMemberSession>()
                .Where(value => value.ExternalMemberId == member.Id && value.TenantId == request.TenantId &&
                    value.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var activeSession in activeSessions)
            {
                activeSession.RevokedAt = now;
                activeSession.ModifiedOn = now;
                session.Store(activeSession);
            }

            session.Store(credential);
            session.Store(member);
            session.Store(reset);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<LocalExternalMemberPasswordResetReceipt, AeroError>(new(request.ReturnPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.ClearChanges();
            return Cancelled<LocalExternalMemberPasswordResetReceipt>();
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return AuthenticationFailure<LocalExternalMemberPasswordResetReceipt>();
        }
        catch
        {
            session.ClearChanges();
            return DatabaseFailure<LocalExternalMemberPasswordResetReceipt>("Local password reset could not be completed.");
        }
    }

    private static string NormalizeEmail(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
    private static string LocalIssuer(long authorityId) =>
        string.Create(CultureInfo.InvariantCulture, $"urn:aerocms:external-member-local-authority:{authorityId}");
    private static ExternalMemberSession CreateSession(long tenantId, long siteId, ExternalMember member,
        ExternalIdentityLink link, DateTimeOffset now) => new()
    {
        Id = Snowflake.NewId(), TenantId = tenantId, SiteId = siteId, ExternalMemberId = member.Id,
        ExternalIdentityLinkId = link.Id, AuthenticationProvider = LocalExternalMemberAuthentication.Provider,
        SecurityVersion = member.SecurityVersion, ExpiresAt = now.Add(SessionLifetime), CreatedOn = now
    };
    private static string CreateSecret() { Span<byte> bytes = stackalloc byte[32]; RandomNumberGenerator.Fill(bytes); return WebEncoders.Base64UrlEncode(bytes); }
    private static string CreateHandle(long id, string secret) => string.Create(CultureInfo.InvariantCulture, $"{id}.{secret}");
    private static string DigestSecret(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
    private static bool VerifySecret(string supplied, string digest) { byte[] expected; try { expected = Convert.FromHexString(digest); } catch (FormatException) { return false; } var actual = SHA256.HashData(Encoding.UTF8.GetBytes(supplied)); return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual); }
    private static bool TryParseHandle(string handle, out long id, out string secret) { id = 0; secret = string.Empty; if (!ExternalMemberIssuanceRules.IsOpaqueHandle(handle)) return false; var index = handle.IndexOf('.'); if (!long.TryParse(handle.AsSpan(0, index), NumberStyles.None, CultureInfo.InvariantCulture, out id)) return false; secret = handle[(index + 1)..]; return true; }
    private static bool IsUniqueConflict(Exception exception) { for (var current = exception; current is not null; current = current.InnerException) { var message = current.Message; if (message.Contains("unique", StringComparison.OrdinalIgnoreCase) && (message.Contains("index", StringComparison.OrdinalIgnoreCase) || message.Contains("constraint", StringComparison.OrdinalIgnoreCase)) || message.Contains("Database index `uidx_", StringComparison.OrdinalIgnoreCase) && message.Contains("already contains", StringComparison.OrdinalIgnoreCase)) return true; } return false; }
    private static Result<T, AeroError> ValidationFailure<T>(IEnumerable<string> errors) => Prelude.Fail<T, AeroError>(AeroError.ValidationError(errors.Distinct(StringComparer.Ordinal)));
    private static Result<T, AeroError> Fail<T>(string message) => Prelude.Fail<T, AeroError>(AeroError.CreateError(message));
    private static Result<T, AeroError> AuthenticationFailure<T>() =>
        Prelude.Fail<T, AeroError>(AeroError.CreateError("Local member authentication could not be completed."));
    private static Result<T, AeroError> Cancelled<T>() => Prelude.Fail<T, AeroError>(AeroError.CancelledError("Operation was cancelled."));
    private static Result<T, AeroError> Conflict<T>() => Prelude.Fail<T, AeroError>(AeroError.ConflictError("Local-member data changed concurrently; restart the operation."));
    private static Result<T, AeroError> DatabaseFailure<T>(string message) => Prelude.Fail<T, AeroError>(AeroError.DatabaseError(message));
}
