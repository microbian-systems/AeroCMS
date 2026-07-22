using Aero.Core;

namespace Aero.Cms.Abstractions.Authentication;

/// <summary>Identifiers and predicates for AeroCMS-managed storefront authentication.</summary>
public static class LocalExternalMemberAuthentication
{
    public const string Provider = "local_identity";
    public const string ActivationRateLimitPolicy = "AeroCms.ExternalMembers.Local.Activation";
    public const string LoginRateLimitPolicy = "AeroCms.ExternalMembers.Local.Login";
    public const string PasswordResetRateLimitPolicy = "AeroCms.ExternalMembers.Local.PasswordReset";
}

/// <summary>Recognizes providers permitted on a local external-member session.</summary>
public static class ExternalMemberSessionProviders
{
    public static bool IsSupported(string? value) =>
        string.Equals(value, LocalExternalMemberAuthentication.Provider, StringComparison.Ordinal) ||
        ExternalMemberProviders.IsSupported(value);
}

public sealed record CreateLocalExternalMemberInvitationRequest(
    long TenantId,
    long SiteId,
    long LocalAuthorityId,
    string Email,
    DateTimeOffset ExpiresAt);

public sealed record ActivateLocalExternalMemberInvitationRequest(
    long TenantId,
    long SiteId,
    string InvitationHandle,
    string Email,
    string Password,
    string? DisplayName,
    string ReturnPath);

public sealed record LoginLocalExternalMemberRequest(
    long TenantId,
    long SiteId,
    string Email,
    string Password,
    string ReturnPath);

public sealed record ResetLocalExternalMemberPasswordRequest(
    long TenantId,
    long SiteId,
    string ResetHandle,
    string NewPassword,
    string ReturnPath);

public sealed record LocalExternalMemberPasswordResetReceipt(string ReturnPath);

public sealed record IssueLocalExternalMemberPasswordResetRequest(
    long TenantId,
    long SiteId,
    long ExternalMemberId,
    long IssuedByManagerUserId,
    DateTimeOffset ExpiresAt);

/// <summary>One-time password-reset handle. Only its digest is persisted.</summary>
public sealed record LocalExternalMemberPasswordResetHandle(string Handle, DateTimeOffset ExpiresAt);

/// <summary>Invitation-gated AeroCMS-managed storefront credential boundary.</summary>
public interface ILocalExternalMemberAuthenticationService
{
    Task<Result<ExternalMemberInvitationHandle, AeroError>> CreateInvitationAsync(
        CreateLocalExternalMemberInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExternalMemberIssuanceReceipt, AeroError>> ActivateInvitationAsync(
        ActivateLocalExternalMemberInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ExternalMemberIssuanceReceipt, AeroError>> LoginAsync(
        LoginLocalExternalMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LocalExternalMemberPasswordResetReceipt, AeroError>> ResetPasswordAsync(
        ResetLocalExternalMemberPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LocalExternalMemberPasswordResetHandle, AeroError>> IssuePasswordResetAsync(
        IssueLocalExternalMemberPasswordResetRequest request,
        CancellationToken cancellationToken = default);
}
