using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Models;

/// <summary>
/// Represents a general file stored in the CMS.
/// </summary>
public class CmsFile : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Path.
    /// </summary>
public string Path { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Size.
    /// </summary>
public long Size { get; set; }
        /// <summary>
    /// Gets or sets the Mime Type.
    /// </summary>
public string MimeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the optional base64 encoded content for small files or stubs.
    /// </summary>
    public string? Content { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
