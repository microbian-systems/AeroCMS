using Aero.Core.Entities;

namespace Aero.Cms.Modules.Banner;

public class BannerModel : Entity
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string Message { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool DisableClose { get; set; }
}