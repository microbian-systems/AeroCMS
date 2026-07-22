using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>One-time authorization to link an exact recovery administrator to a manager provider identity.</summary>
public sealed class ManagerFederationLinkIntent : SableDocument, IAuditable, IVersioned
{
    public long AuthorityBindingId { get; set; }
    public long RecoveryAdministratorUserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string SecretDigest { get; set; } = string.Empty;
    public string CallbackUri { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
