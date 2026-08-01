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
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

/// <summary>Coordinates provider-neutral, invitation-gated provisioning and returning-member issuance.</summary>
public sealed class ExternalMemberIssuanceService(
    IDocumentSession session,
    IValidator<CreateExternalMemberInvitationRequest> invitationValidator,
    IValidator<BeginExternalMemberSignInRequest> beginValidator,
    IValidator<CompleteExternalMemberSignInRequest> completeValidator,
    TimeProvider timeProvider) : IExternalMemberIssuanceService
{
    private static readonly TimeSpan AuthenticationStateLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MemberSessionLifetime = TimeSpan.FromHours(8);

    public async Task<Result<ExternalMemberInvitationHandle, AeroError>> CreateInvitationAsync(
        CreateExternalMemberInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await invitationValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return ValidationFailure<ExternalMemberInvitationHandle>(validation.Errors.Select(error => error.ErrorMessage));

            var binding = await session.LoadAsync<ExternalOrganizationBinding>(request.OrganizationBindingId, cancellationToken);
            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            if (!IsMatchingActiveBinding(binding, request.TenantId, request.Provider) ||
                !IsMatchingActiveSite(site, request.TenantId, request.SiteId))
                return Fail<ExternalMemberInvitationHandle>("Invitation scope is unavailable.");

            var secret = CreateSecret();
            var invitation = new ExternalMemberInvitation
            {
                Id = Snowflake.NewId(),
                TenantId = request.TenantId,
                SiteId = request.SiteId,
                OrganizationBindingId = binding!.Id,
                LocalAuthorityId = null,
                Provider = request.Provider,
                NormalizedEmail = NormalizeEmail(request.Email),
                TokenDigest = DigestSecret(secret),
                ExpiresAt = request.ExpiresAt,
                CreatedOn = timeProvider.GetUtcNow()
            };
            session.Store(invitation);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ExternalMemberInvitationHandle, AeroError>(
                new(CreateHandle(invitation.Id, secret), invitation.ExpiresAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.ClearChanges();
            return Cancelled<ExternalMemberInvitationHandle>();
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict<ExternalMemberInvitationHandle>();
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            session.ClearChanges();
            return Conflict<ExternalMemberInvitationHandle>();
        }
        catch
        {
            session.ClearChanges();
            return DatabaseFailure<ExternalMemberInvitationHandle>("Invitation could not be created.");
        }
    }

    public async Task<Result<ExternalMemberAuthenticationHandle, AeroError>> BeginAsync(
        BeginExternalMemberSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await beginValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return ValidationFailure<ExternalMemberAuthenticationHandle>(validation.Errors.Select(error => error.ErrorMessage));

            long? invitationId = null;
            string? invitationSecret = null;
            if (request.InvitationHandle is not null)
            {
                if (!TryParseHandle(request.InvitationHandle, out var parsedInvitationId, out invitationSecret))
                    return Fail<ExternalMemberAuthenticationHandle>("Sign-in could not be started.");
                invitationId = parsedInvitationId;
            }

            var now = timeProvider.GetUtcNow();
            var binding = await session.LoadAsync<ExternalOrganizationBinding>(request.OrganizationBindingId, cancellationToken);
            var site = await session.LoadAsync<SitesModel>(request.SiteId, cancellationToken);
            ExternalMemberInvitation? invitation = invitationId.HasValue
                ? await session.LoadAsync<ExternalMemberInvitation>(invitationId.Value, cancellationToken)
                : null;

            if (!IsMatchingActiveBinding(binding, request.TenantId, request.Provider) ||
                !IsMatchingActiveSite(site, request.TenantId, request.SiteId) ||
                (invitationId.HasValue &&
                 (!IsUsableInvitation(invitation, request.TenantId, request.SiteId, request.OrganizationBindingId,
                      request.Provider, now) || !VerifySecret(invitationSecret!, invitation!.TokenDigest))))
                return Fail<ExternalMemberAuthenticationHandle>("Sign-in could not be started.");

            var secret = CreateSecret();
            var state = new ExternalAuthenticationState
            {
                Id = Snowflake.NewId(),
                TenantId = request.TenantId,
                SiteId = request.SiteId,
                OrganizationBindingId = request.OrganizationBindingId,
                ExternalMemberInvitationId = invitationId,
                Provider = request.Provider,
                Purpose = ExternalAuthenticationState.SignInPurpose,
                SecretDigest = DigestSecret(secret),
                ReturnPath = request.ReturnPath,
                ProtectedProviderCorrelation = request.ProtectedProviderCorrelation,
                ExpiresAt = now.Add(AuthenticationStateLifetime),
                CreatedOn = now
            };
            session.Store(state);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ExternalMemberAuthenticationHandle, AeroError>(
                new(CreateHandle(state.Id, secret), state.ReturnPath, state.ExpiresAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.ClearChanges();
            return Cancelled<ExternalMemberAuthenticationHandle>();
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict<ExternalMemberAuthenticationHandle>();
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            session.ClearChanges();
            return Conflict<ExternalMemberAuthenticationHandle>();
        }
        catch
        {
            session.ClearChanges();
            return DatabaseFailure<ExternalMemberAuthenticationHandle>("Sign-in could not be started.");
        }
    }

    /// <summary>Validates a callback handle and its persisted local scope without consuming the state.</summary>
    public async Task<Result<ExternalMemberCallbackPreparation, AeroError>> PrepareCallbackAsync(
        string authenticationHandle,
        long expectedTenantId,
        long expectedSiteId,
        string expectedProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryParseHandle(authenticationHandle, out var stateId, out var stateSecret) ||
                expectedTenantId <= 0 || expectedSiteId <= 0 || !ExternalMemberIssuanceRules.IsCanonicalProvider(expectedProvider))
                return Fail<ExternalMemberCallbackPreparation>("External sign-in could not be prepared.");

            var state = await session.LoadAsync<ExternalAuthenticationState>(stateId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            if (!IsUsablePreparedState(state, expectedTenantId, expectedSiteId, expectedProvider, now) ||
                !VerifySecret(stateSecret, state!.SecretDigest))
                return Fail<ExternalMemberCallbackPreparation>("External sign-in could not be prepared.");

            var site = await session.LoadAsync<SitesModel>(state.SiteId, cancellationToken);
            var binding = await session.LoadAsync<ExternalOrganizationBinding>(state.OrganizationBindingId, cancellationToken);
            if (!IsMatchingActiveSite(site, expectedTenantId, expectedSiteId) ||
                !IsUsableCallbackBinding(binding, expectedTenantId, expectedProvider))
                return Fail<ExternalMemberCallbackPreparation>("External sign-in could not be prepared.");

            return Prelude.Ok<ExternalMemberCallbackPreparation, AeroError>(new(
                state.OrganizationBindingId, state.ProtectedProviderCorrelation, state.ReturnPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled<ExternalMemberCallbackPreparation>();
        }
        catch
        {
            return DatabaseFailure<ExternalMemberCallbackPreparation>("External sign-in could not be prepared.");
        }
    }

    public async Task<Result<ExternalMemberCallbackPreparationWithProvider, AeroError>> PrepareCallbackAsync(
        string authenticationHandle, long expectedTenantId, long expectedSiteId, CancellationToken cancellationToken = default)
    {
        if (!TryParseHandle(authenticationHandle, out var stateId, out _) || expectedTenantId <= 0 || expectedSiteId <= 0)
            return Fail<ExternalMemberCallbackPreparationWithProvider>("External sign-in could not be prepared.");
        try
        {
            var state = await session.LoadAsync<ExternalAuthenticationState>(stateId, cancellationToken);
            if (state is null || !ExternalMemberIssuanceRules.IsCanonicalProvider(state.Provider))
                return Fail<ExternalMemberCallbackPreparationWithProvider>("External sign-in could not be prepared.");
            var prepared = await PrepareCallbackAsync(authenticationHandle, expectedTenantId, expectedSiteId, state.Provider, cancellationToken);
            return prepared is Result<ExternalMemberCallbackPreparation, AeroError>.Ok(var value)
                ? Prelude.Ok<ExternalMemberCallbackPreparationWithProvider, AeroError>(new(value.OrganizationBindingId, state.Provider, value.ProtectedProviderCorrelation, value.ReturnPath))
                : Fail<ExternalMemberCallbackPreparationWithProvider>("External sign-in could not be prepared.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Cancelled<ExternalMemberCallbackPreparationWithProvider>(); }
        catch { return DatabaseFailure<ExternalMemberCallbackPreparationWithProvider>("External sign-in could not be prepared."); }
    }

    public async Task<Result<ExternalMemberIssuanceReceipt, AeroError>> CompleteAsync(
        CompleteExternalMemberSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await completeValidator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return ValidationFailure<ExternalMemberIssuanceReceipt>(validation.Errors.Select(error => error.ErrorMessage));
            if (!TryParseHandle(request.AuthenticationHandle, out var stateId, out var stateSecret))
                return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");

            var now = timeProvider.GetUtcNow();
            var state = await session.LoadAsync<ExternalAuthenticationState>(stateId, cancellationToken);
            if (!IsUsableState(state, request, now) || !VerifySecret(stateSecret, state!.SecretDigest))
                return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");

            var site = await session.LoadAsync<SitesModel>(state.SiteId, cancellationToken);
            var binding = await session.LoadAsync<ExternalOrganizationBinding>(state.OrganizationBindingId, cancellationToken);
            var identity = request.Identity;
            if (!IsMatchingActiveSite(site, state.TenantId, state.SiteId) ||
                !IsMatchingActiveBinding(binding, state.TenantId, state.Provider) ||
                !string.Equals(identity.Provider, state.Provider, StringComparison.Ordinal) ||
                !string.Equals(identity.Issuer, binding!.Issuer, StringComparison.Ordinal) ||
                !string.Equals(identity.OrganizationId, binding.OrganizationId, StringComparison.Ordinal))
                return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");

            // Resolve the exact external identity before deciding whether an invitation is required.
            var identityKey = ComputeKey(identity.Provider, identity.Issuer, identity.Subject);
            var link = await session.Query<ExternalIdentityLink>()
                .FirstOrDefaultAsync(candidate => candidate.IdentityKey == identityKey, cancellationToken);
            ExternalMember? member = null;
            ExternalMemberSiteAssignment? assignment = null;

            if (link is not null)
            {
                if (!link.IsActive ||
                    !string.Equals(link.Provider, identity.Provider, StringComparison.Ordinal) ||
                    !string.Equals(link.Issuer, identity.Issuer, StringComparison.Ordinal) ||
                    !string.Equals(link.Subject, identity.Subject, StringComparison.Ordinal))
                    return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");

                member = await session.LoadAsync<ExternalMember>(link.ExternalMemberId, cancellationToken);
                if (member is not { IsActive: true })
                    return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");

                assignment = await session.Query<ExternalMemberSiteAssignment>()
                    .FirstOrDefaultAsync(candidate =>
                        candidate.ExternalMemberId == member.Id && candidate.SiteId == state.SiteId,
                        cancellationToken);
                if (assignment is { IsActive: false } ||
                    assignment is not null && assignment.TenantId != state.TenantId)
                    return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");
            }

            var invitationRequired = link is null || assignment is null;
            ExternalMemberInvitation? invitation = state.ExternalMemberInvitationId.HasValue
                ? await session.LoadAsync<ExternalMemberInvitation>(state.ExternalMemberInvitationId.Value, cancellationToken)
                : null;
            var invitationMustBeValidated = invitationRequired || state.ExternalMemberInvitationId.HasValue;
            if (invitationMustBeValidated &&
                (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email) ||
                 !IsUsableInvitation(invitation, state.TenantId, state.SiteId, state.OrganizationBindingId,
                     state.Provider, now) ||
                 !string.Equals(NormalizeEmail(identity.Email), invitation!.NormalizedEmail, StringComparison.Ordinal)))
                return Fail<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");

            if (link is null)
            {
                member = new ExternalMember
                {
                    Id = Snowflake.NewId(),
                    IsActive = true,
                    SecurityVersion = 1,
                    DisplayName = identity.DisplayName,
                    Email = identity.Email,
                    CreatedOn = now
                };
                link = new ExternalIdentityLink
                {
                    Id = Snowflake.NewId(),
                    Provider = identity.Provider,
                    Issuer = identity.Issuer,
                    Subject = identity.Subject,
                    IdentityKey = identityKey,
                    ExternalMemberId = member.Id,
                    ExternalMemberInvitationId = invitation!.Id,
                    IsActive = true,
                    CreatedOn = now
                };
                session.Store(member);
                session.Store(link);
            }

            if (assignment is null)
            {
                assignment = new ExternalMemberSiteAssignment
                {
                    Id = Snowflake.NewId(),
                    ExternalMemberId = member!.Id,
                    TenantId = state.TenantId,
                    SiteId = state.SiteId,
                    IsActive = true,
                    CreatedOn = now
                };
                session.Store(assignment);
            }

            var localSession = new ExternalMemberSession
            {
                Id = Snowflake.NewId(),
                TenantId = state.TenantId,
                SiteId = state.SiteId,
                ExternalMemberId = member!.Id,
                ExternalIdentityLinkId = link.Id,
                AuthenticationProvider = state.Provider,
                ProviderSessionReference = identity.ProviderSessionReference,
                SecurityVersion = member.SecurityVersion,
                ExpiresAt = now.Add(MemberSessionLifetime),
                CreatedOn = now
            };
            state.ConsumedAt = now;
            state.ModifiedOn = now;
            session.Store(state);
            session.Store(localSession);

            if (invitationMustBeValidated)
            {
                invitation!.ConsumedAt = now;
                invitation.ConsumedByExternalMemberId = member.Id;
                invitation.ModifiedOn = now;
                session.Store(invitation);
            }

            // The sole local issuance save is one Sable transaction. The receipt is created only after commit.
            await session.SaveChangesAsync(cancellationToken);

            return Prelude.Ok<ExternalMemberIssuanceReceipt, AeroError>(new(
                member.Id,
                link.Id,
                localSession.Id,
                state.TenantId,
                state.SiteId,
                state.Provider,
                member.SecurityVersion,
                localSession.ExpiresAt,
                state.ReturnPath));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.ClearChanges();
            return Cancelled<ExternalMemberIssuanceReceipt>();
        }
        catch (ConcurrencyException)
        {
            session.ClearChanges();
            return Conflict<ExternalMemberIssuanceReceipt>();
        }
        catch (Exception exception) when (IsUniqueConstraintConflict(exception))
        {
            session.ClearChanges();
            return Conflict<ExternalMemberIssuanceReceipt>();
        }
        catch
        {
            session.ClearChanges();
            return DatabaseFailure<ExternalMemberIssuanceReceipt>("External sign-in could not be completed.");
        }
    }

    internal static string ComputeKey(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return $"v1.{WebEncoders.Base64UrlEncode(hash.GetHashAndReset())}";
    }

    private static bool IsMatchingActiveBinding(ExternalOrganizationBinding? binding, long tenantId, string provider) =>
        binding is { IsActive: true } && binding.TenantId == tenantId &&
        string.Equals(binding.Provider, provider, StringComparison.Ordinal) &&
        ExternalMemberIssuanceRules.IsExactHttpsIssuer(binding.Issuer) &&
        ExternalMemberIssuanceRules.IsExactOpaqueValue(binding.OrganizationId) &&
        string.Equals(binding.BindingKey,
            ComputeKey(binding.Provider, binding.Issuer, binding.OrganizationId), StringComparison.Ordinal);

    private static bool IsUsableCallbackBinding(ExternalOrganizationBinding? binding, long tenantId, string provider) =>
        IsMatchingActiveBinding(binding, tenantId, provider) &&
        ExternalMemberIssuanceRules.IsExactHttpsIssuer(binding!.Authority) &&
        binding.VaultId > 0 && IsVaultEnvironment(binding.VaultEnvironment) &&
        string.Equals(binding.CredentialPath,
            ExternalProviderSecretReference.CanonicalCredentialPath(tenantId, provider), StringComparison.Ordinal);

    private static bool IsMatchingActiveSite(SitesModel? site, long tenantId, long siteId) =>
        site is { IsEnabled: true } && site.Id == siteId && site.TenantId == tenantId;

    private static bool IsUsableInvitation(
        ExternalMemberInvitation? invitation,
        long tenantId,
        long siteId,
        long bindingId,
        string provider,
        DateTimeOffset now) =>
        invitation is not null && invitation.TenantId == tenantId && invitation.SiteId == siteId &&
        invitation.OrganizationBindingId == bindingId &&
        invitation.LocalAuthorityId is null &&
        string.Equals(invitation.Provider, provider, StringComparison.Ordinal) &&
        invitation.ConsumedAt is null && invitation.RevokedAt is null && invitation.ExpiresAt > now;

    private static bool IsUsableState(
        ExternalAuthenticationState? state,
        CompleteExternalMemberSignInRequest request,
        DateTimeOffset now) =>
        state is not null && state.TenantId == request.TenantId && state.SiteId == request.SiteId &&
        string.Equals(state.Provider, request.Provider, StringComparison.Ordinal) &&
        string.Equals(state.Purpose, ExternalAuthenticationState.SignInPurpose, StringComparison.Ordinal) &&
        ExternalMemberIssuanceRules.IsSafeLocalReturnPath(state.ReturnPath) &&
        state.ConsumedAt is null && state.ExpiresAt > now;

    private static bool IsUsablePreparedState(
        ExternalAuthenticationState? state,
        long tenantId,
        long siteId,
        string provider,
        DateTimeOffset now) =>
        state is not null && state.TenantId == tenantId && state.SiteId == siteId &&
        string.Equals(state.Provider, provider, StringComparison.Ordinal) &&
        string.Equals(state.Purpose, ExternalAuthenticationState.SignInPurpose, StringComparison.Ordinal) &&
        ExternalMemberIssuanceRules.IsSafeLocalReturnPath(state.ReturnPath) &&
        ExternalMemberIssuanceRules.IsProtectedProviderCorrelation(state.ProtectedProviderCorrelation) &&
        state.ConsumedAt is null && state.ExpiresAt > now;

    private static bool IsVaultEnvironment(string? value) =>
        value is { Length: > 0 and <= 128 } && string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    private static string NormalizeEmail(string value) =>
        value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string CreateHandle(long id, string secret) =>
        string.Create(CultureInfo.InvariantCulture, $"{id}.{secret}");

    private static bool TryParseHandle(string handle, out long id, out string secret)
    {
        id = 0;
        secret = string.Empty;
        if (!ExternalMemberIssuanceRules.IsOpaqueHandle(handle)) return false;
        var separator = handle.IndexOf('.');
        if (!long.TryParse(handle.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out id))
            return false;
        secret = handle[(separator + 1)..];
        return true;
    }

    private static string DigestSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static bool VerifySecret(string supplied, string persistedDigest)
    {
        byte[] persisted;
        try { persisted = Convert.FromHexString(persistedDigest); }
        catch (FormatException) { return false; }
        var suppliedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return persisted.Length == suppliedDigest.Length &&
            CryptographicOperations.FixedTimeEquals(persisted, suppliedDigest);
    }

    private static bool IsUniqueConstraintConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if ((message.Contains("unique", StringComparison.OrdinalIgnoreCase) &&
                 (message.Contains("index", StringComparison.OrdinalIgnoreCase) ||
                  message.Contains("constraint", StringComparison.OrdinalIgnoreCase))) ||
                (message.Contains("Database index `uidx_", StringComparison.OrdinalIgnoreCase) &&
                 message.Contains("already contains", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static Result<T, AeroError> ValidationFailure<T>(IEnumerable<string> errors) =>
        Prelude.Fail<T, AeroError>(AeroError.ValidationError(errors.Distinct(StringComparer.Ordinal)));

    private static Result<T, AeroError> Fail<T>(string message) =>
        Prelude.Fail<T, AeroError>(AeroError.CreateError(message));

    private static Result<T, AeroError> Cancelled<T>() =>
        Prelude.Fail<T, AeroError>(AeroError.CancelledError("Operation was cancelled."));

    private static Result<T, AeroError> Conflict<T>() =>
        Prelude.Fail<T, AeroError>(AeroError.ConflictError("External-member data changed concurrently; restart the operation."));

    private static Result<T, AeroError> DatabaseFailure<T>(string message) =>
        Prelude.Fail<T, AeroError>(AeroError.DatabaseError(message));
}
