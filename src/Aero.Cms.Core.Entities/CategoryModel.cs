using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;


/// <summary>
/// Stores a mutable category label, route slug, optional description, and optional parent-category reference.
/// </summary>
public class CategoryModel : SableDocument, IAuditable
{
    // IAuditable
    /// <summary>Gets or sets the creation timestamp. The default is UTC, but setters do not enforce an offset.</summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Gets or sets the last-modified timestamp; callers and persistence conventionally use UTC, but setters do not enforce it.</summary>
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <summary>Gets or sets the actor recorded as creating this document, when available.</summary>
    public string? CreatedBy { get; set; }
    /// <summary>Gets or sets the actor recorded as last modifying this document, when available.</summary>
    public string? ModifiedBy { get; set; }

        /// <summary>
    /// Gets or sets the caller-supplied category display label; this document does not require a value.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the optional route segment; normalization and uniqueness are external concerns.
    /// </summary>
public string? Slug { get; set; }
        /// <summary>
    /// Gets or sets optional descriptive text stored with the category.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets an optional identifier representing a parent category; no hierarchy relationship is enforced here.
    /// </summary>
public long? ParentCategoryId { get; set; }
}
