using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Represents a one-time local storefront password-reset grant.</summary>
public sealed class ExternalMemberPasswordReset : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long CredentialId { get; set; }
    public string TokenDigest { get; set; } = string.Empty;
    public long CapturedCredentialSecurityVersion { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public long IssuedByManagerUserId { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
