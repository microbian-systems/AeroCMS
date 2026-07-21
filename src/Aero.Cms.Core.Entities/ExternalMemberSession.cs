using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Represents a revocable local session issued after external authentication.</summary>
public sealed class ExternalMemberSession : SableDocument, IAuditable, IVersioned
{
    /// <summary>Gets or sets the owning local external member identifier.</summary>
    public long ExternalMemberId { get; set; }

    /// <summary>Gets or sets the exact active external identity link used for issuance.</summary>
    public long ExternalIdentityLinkId { get; set; }

    /// <summary>Gets or sets the nonblank provider name that issued this local session.</summary>
    public string AuthenticationProvider { get; set; } = string.Empty;

    /// <summary>Gets or sets the opaque upstream session reference, when supplied.</summary>
    public string? ProviderSessionReference { get; set; }

    /// <summary>Gets or sets the member security version captured when this session was issued.</summary>
    public long SecurityVersion { get; set; }

    /// <summary>Gets or sets when this session expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Gets or sets when this session was revoked, if it has been revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
