using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>Represents a local storefront customer or partner principal.</summary>
public sealed class ExternalMember : SableDocument, IAuditable, IVersioned
{
    /// <summary>Gets or sets whether this local member may authenticate.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the monotonically increasing value used to invalidate sessions.</summary>
    public long SecurityVersion { get; set; } = 1;

    /// <summary>Gets or sets the provider-supplied display-name snapshot.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the provider-supplied email snapshot, when available.</summary>
    public string? Email { get; set; }

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}
