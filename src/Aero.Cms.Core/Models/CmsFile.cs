using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Models;

/// <summary>
/// Represents a general file stored in the CMS.
/// </summary>
public class CmsFile : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the file name.
    /// </summary>
public string Name { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the storage path for the file.
    /// </summary>
public string Path { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
public long Size { get; set; }
        /// <summary>
    /// Gets or sets the file's MIME type.
    /// </summary>
public string MimeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the optional base64 encoded content for small files or stubs.
    /// </summary>
    public string? Content { get; set; }

    // IAuditable
    /// <summary>Gets or sets the audit creation timestamp.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the most recent audit modification timestamp.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the identity that created this file, if recorded.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the identity that last modified this file, if recorded.</summary>
    public string? ModifiedBy { get; set; }
}
