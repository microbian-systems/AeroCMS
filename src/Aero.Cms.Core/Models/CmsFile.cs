using Aero.Core.Entities;

namespace Aero.Cms.Core.Models;

/// <summary>
/// Represents a general file stored in the CMS.
/// </summary>
public class CmsFile : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string MimeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the optional base64 encoded content for small files or stubs.
    /// </summary>
    public string? Content { get; set; }
}
