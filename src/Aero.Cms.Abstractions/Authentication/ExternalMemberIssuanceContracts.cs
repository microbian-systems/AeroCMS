using Aero.Core;

namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Creates an invite-only storefront membership grant.</summary>
public sealed record CreateExternalMemberInvitationRequest(
    long TenantId,
    long SiteId,
    long OrganizationBindingId,
    string Provider,
    string Email,
    DateTimeOffset ExpiresAt);

/// <summary>One-time invitation handle. The secret is returned only at creation time.</summary>
public sealed record ExternalMemberInvitationHandle(string Handle, DateTimeOffset ExpiresAt);

/// <summary>Begins a provider sign-in bound to an invitation and local tenant/site context.</summary>
public sealed record BeginExternalMemberSignInRequest(
    long TenantId,
    long SiteId,
    long OrganizationBindingId,
    string? InvitationHandle,
    string Provider,
    string ReturnPath,
    string ProtectedProviderCorrelation);

/// <summary>One-time callback-state handle. The secret is returned only to the caller.</summary>
public sealed record ExternalMemberAuthenticationHandle(
    string Handle,
    string ReturnPath,
    DateTimeOffset ExpiresAt);

/// <summary>Validated callback-start context. Reading it does not consume the one-time state.</summary>
public sealed record ExternalMemberCallbackPreparation(
    long OrganizationBindingId,
    string ProtectedProviderCorrelation,
    string ReturnPath);

/// <summary>Identity assertions already validated cryptographically by a provider adapter.</summary>
public sealed record ValidatedExternalIdentity(
    string Provider,
    string Issuer,
    string Subject,
    string OrganizationId,
    string? Email,
    bool EmailVerified,
    string? DisplayName,
    string? ProviderSessionReference,
    DateTimeOffset ValidatedAt);

/// <summary>Completes a provider-neutral callback without exposing ASP.NET authentication types.</summary>
public sealed record CompleteExternalMemberSignInRequest(
    string AuthenticationHandle,
    long TenantId,
    long SiteId,
    string Provider,
    ValidatedExternalIdentity Identity);

/// <summary>Immutable receipt returned only after local issuance has committed.</summary>
public sealed record ExternalMemberIssuanceReceipt(
    long ExternalMemberId,
    long ExternalIdentityLinkId,
    long ExternalMemberSessionId,
    long TenantId,
    long SiteId,
    string Provider,
    long SecurityVersion,
    DateTimeOffset ExpiresAt,
    string ReturnPath);

/// <summary>Provider-neutral local invitation, callback-state, and session issuance boundary.</summary>
public interface IExternalMemberIssuanceService
{
    Task<Result<ExternalMemberInvitationHandle, AeroError>> CreateInvitationAsync(
        CreateExternalMemberInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExternalMemberAuthenticationHandle, AeroError>> BeginAsync(
        BeginExternalMemberSignInRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExternalMemberCallbackPreparation, AeroError>> PrepareCallbackAsync(
        string authenticationHandle,
        long expectedTenantId,
        long expectedSiteId,
        string expectedProvider,
        CancellationToken cancellationToken = default);

    Task<Result<ExternalMemberCallbackPreparationWithProvider, AeroError>> PrepareCallbackAsync(
        string authenticationHandle, long expectedTenantId, long expectedSiteId, CancellationToken cancellationToken = default);

    Task<Result<ExternalMemberIssuanceReceipt, AeroError>> CompleteAsync(
        CompleteExternalMemberSignInRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalMemberCallbackPreparationWithProvider(long OrganizationBindingId,
    string Provider, string ProtectedProviderCorrelation, string ReturnPath);
