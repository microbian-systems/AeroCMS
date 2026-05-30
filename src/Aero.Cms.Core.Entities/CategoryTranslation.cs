using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

public sealed class CategoryTranslation : Entity, ICultureAware
{
    public long CategoryId { get; set; }
    public string Culture { get; set; } = SitesModel.DefaultCultureName;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
}
