using Aero.Core.Data;
using AeroDB.Sable;

using System.Globalization;

namespace Aero.Cms.Core.Entities;

/// <summary>
/// Stores a culture-scoped CMS URL alias from a source path to a redirect target path.
/// </summary>
/// <remarks>
/// The document is a mutable storage shape. Callers are responsible for supplying normalized path and culture values
/// when required by persistence or routing consumers; the property setters do not validate or normalize them.
/// </remarks>
public class AliasDocument : SableDocument, IAuditable
{
    /// <summary>
    /// Gets or sets the site identifier that scopes this alias; this type does not enforce site isolation.
    /// </summary>
    public long SiteId { get; set; }

    /// <summary>Gets or sets the culture label that scopes the source route; the setter does not normalize it.</summary>
    public string Culture { get; set; } = "en-US";

    /// <summary>Gets or sets the identifier of the content that owns this alias.</summary>
    public long? OwnerId { get; set; }

    /// <summary>Gets or sets the owner kind. Automatic page aliases use <c>Page</c>.</summary>
    public string? OwnerType { get; set; }

    /// <summary>Gets or sets whether this alias is maintained by a content route.</summary>
    public bool IsAutomatic { get; set; }
    /// <summary>
    /// Gets or sets the CMS URL path that should be matched as the alias source.
    /// </summary>
    public string OldPath { get; set; } = null!;

    /// <summary>
    /// Gets or sets the normalized source path used by the persistence uniqueness index and
    /// <c>PageRouteAliasWriter</c> lookups.
    /// </summary>
    /// <remarks>
    /// Runtime <c>AliasRuleCache</c> entries are built by normalizing <see cref="OldPath"/> directly; that cache does
    /// not read this property.
    /// </remarks>
    public string NormalizedOldPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets the CMS URL path used as the redirect target.
    /// </summary>
    public string NewPath { get; set; } = null!;

    /// <summary>Gets or sets the caller-supplied redirect status code, defaulting to 301; this type does not validate it.</summary>
    public int StatusCode { get; set; } = 301;
    /// <summary>
    /// Gets or sets optional free-form notes associated with the alias.
    /// </summary>
    public string? Notes { get; set; } = null!;

    // IAuditable
    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }

    /// <summary>Normalizes a culture-neutral public path.</summary>
    /// <param name="path">The path to normalize. <see langword="null"/>, empty, and root paths produce <c>/</c>.</param>
    /// <returns>A slash-prefixed, lower-invariant path with no trailing slash unless it is the root path.</returns>
    public static string NormalizePath(string? path)
    {
        var normalized = (path ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized == "/")
            return "/";

        return "/" + normalized.Trim('/').ToLowerInvariant();
    }

    /// <summary>Normalizes an alias culture to its canonical .NET name.</summary>
    /// <param name="culture">The culture name to normalize.</param>
    /// <returns>The canonical culture name, or <c>en-US</c> when <paramref name="culture"/> is blank or invalid.</returns>
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
