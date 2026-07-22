using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Stores the password verifier and lockout state for one local storefront member.</summary>
public sealed class ExternalMemberLocalCredential : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long ExternalMemberId { get; set; }
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int FailedAccessCount { get; set; }
    public DateTimeOffset? LockoutEndUtc { get; set; }
    public long SecurityVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
