using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Core.Entities;


/// <summary>
/// Represents a class for CategoryModel.
/// </summary>
public class CategoryModel : SableDocument, IAuditable
{
    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string? Slug { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Parent Category Id.
    /// </summary>
public long? ParentCategoryId { get; set; }
}
