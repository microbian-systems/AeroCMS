using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.A2A.Models;

/// <summary>Stores the A2A availability switch for one persisted Commerce site.</summary>
public sealed class A2ASettingsDocument : SableDocument, IAuditable
{
    /// <summary>Gets or sets the owning tenant identifier.</summary>
    public long TenantId { get; set; }

    /// <summary>Gets or sets the owning site identifier.</summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets whether the site's A2A surface is available.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the last-modified timestamp.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <summary>Gets or sets the actor that created the setting.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the actor that last changed the setting.</summary>
    public string? ModifiedBy { get; set; }
}
