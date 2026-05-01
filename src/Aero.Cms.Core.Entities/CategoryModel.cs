using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;


public class CategoryModel :Entity
{
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public long? ParentCategoryId { get; set; }
}
