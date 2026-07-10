using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a class for TenantModel.
/// </summary>
public class TenantModel : Entity
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
}