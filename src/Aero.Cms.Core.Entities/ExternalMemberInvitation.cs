using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>One-time invite-only authorization to join one tenant/site.</summary>
public sealed class ExternalMemberInvitation : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    /// <summary>Gets or sets the remote authority binding, exclusively for remote-provider invitations.</summary>
    public long? OrganizationBindingId { get; set; }

    /// <summary>Gets or sets the local authority, exclusively for local-identity invitations.</summary>
    public long? LocalAuthorityId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string TokenDigest { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public long? ConsumedByExternalMemberId { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
