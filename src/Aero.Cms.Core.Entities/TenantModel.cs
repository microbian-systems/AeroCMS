using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a class for TenantModel.
/// </summary>
public class TenantModel : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Hostname.
    /// </summary>
public string Hostname { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Notes.
    /// </summary>
    public string? Notes { get; set; } = null;

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}