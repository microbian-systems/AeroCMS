using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>One-time provider callback state bound to local issuance context.</summary>
public sealed class ExternalAuthenticationState : SableDocument, IAuditable, IVersioned
{
    public const string SignInPurpose = "sign_in";

    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public long OrganizationBindingId { get; set; }
    public long? ExternalMemberInvitationId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = SignInPurpose;
    public string SecretDigest { get; set; } = string.Empty;
    public string ReturnPath { get; set; } = "/";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
