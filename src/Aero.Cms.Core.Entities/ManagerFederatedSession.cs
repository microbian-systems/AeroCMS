using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Revocable local record of a manager session established by a federated login.</summary>
public sealed class ManagerFederatedSession : SableDocument, IAuditable, IVersioned
{
    public long UserId { get; set; }
    public long AuthorityBindingId { get; set; }
    public string LoginProvider { get; set; } = string.Empty;
    public string ProviderKeyDigest { get; set; } = string.Empty;
    public string? ProviderSessionReference { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
