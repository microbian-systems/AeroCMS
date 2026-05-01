using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a URL alias mapping for a site, including the original and new paths, as well as optional notes.
/// </summary>
/// <remarks>Use this class to store or retrieve information about path redirections or rewrites within a site.
/// Each instance associates an old path with a new path for a specific site, which can be useful for managing legacy
/// URLs or implementing custom routing.</remarks>
public class AliasDocument : Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for the site.
    /// </summary>
    public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the original file or directory path before a rename or move operation.
    /// </summary>
    public string OldPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets the new file or directory path to be used in the operation.
    /// </summary>
    public string NewPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets optional notes or comments associated with the object.
    /// </summary>
    public string? Notes { get; set; } = null!;
}