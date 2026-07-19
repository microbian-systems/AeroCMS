using Aero.Core.Data;
using AeroDB.Sable;

using System.Globalization;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents a URL alias mapping for a site, including the original and new paths, as well as optional notes.
/// </summary>
/// <remarks>Use this class to store or retrieve information about path redirections or rewrites within a site.
/// Each instance associates an old path with a new path for a specific site, which can be useful for managing legacy
/// URLs or implementing custom routing.</remarks>
public class AliasDocument : SableDocument, IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for the site.
    /// </summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets the culture that owns this route.</summary>
    public string Culture { get; set; } = "en-US";

    /// <summary>Gets or sets the identifier of the content that owns this alias.</summary>
    public long? OwnerId { get; set; }

    /// <summary>Gets or sets the owner kind. Automatic page aliases use <c>Page</c>.</summary>
    public string? OwnerType { get; set; }

    /// <summary>Gets or sets whether this alias is maintained by a content route.</summary>
    public bool IsAutomatic { get; set; }
    /// <summary>
    /// Gets or sets the original file or directory path before a rename or move operation.
    /// </summary>
    public string OldPath { get; set; } = null!;

    /// <summary>Gets or sets the normalized old path used by persistence and cache keys.</summary>
    public string NormalizedOldPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets the new file or directory path to be used in the operation.
    /// </summary>
    public string NewPath { get; set; } = null!;

    /// <summary>Gets or sets the redirect status code.</summary>
    public int StatusCode { get; set; } = 301;
    /// <summary>
    /// Gets or sets optional notes or comments associated with the object.
    /// </summary>
    public string? Notes { get; set; } = null!;

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Normalizes a culture-neutral public path.</summary>
    public static string NormalizePath(string? path)
    {
        var normalized = (path ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized == "/")
            return "/";

        return "/" + normalized.Trim('/').ToLowerInvariant();
    }

    /// <summary>Normalizes an alias culture to its canonical .NET name.</summary>
    public static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return "en-US";

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return "en-US";
        }
    }
}
