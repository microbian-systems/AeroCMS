using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Grants a local external member storefront access to one site within one tenant.</summary>
public sealed class ExternalMemberSiteAssignment : SableDocument, IAuditable
{
    /// <summary>Gets or sets the local external member identifier.</summary>
    public long ExternalMemberId { get; set; }

    /// <summary>Gets or sets the owning tenant identifier.</summary>
    public long TenantId { get; set; }

    /// <summary>Gets or sets the storefront site identifier.</summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets whether this local site membership is active.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
