using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Binds one exact external issuer/subject identity to one local member.</summary>
public sealed class ExternalIdentityLink : SableDocument, IAuditable, IVersioned
{
    public string Provider { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string IdentityKey { get; set; } = string.Empty;
    public long ExternalMemberId { get; set; }
    public long ExternalMemberInvitationId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
