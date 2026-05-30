using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Entities;

public sealed class ProductTranslation : Entity, ICultureAware
{
    public long ProductId { get; set; }
    public string Culture { get; set; } = SitesModel.DefaultCultureName;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
}
