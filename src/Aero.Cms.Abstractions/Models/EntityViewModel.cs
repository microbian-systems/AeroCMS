using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

public abstract record EntityViewModel : IEntity, ISiteOwned
{
    public long Id { get; set; }
    public long SiteId { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? ModifiedOn { get; set; }
    public string CreatedBy { get; set; }
    public string ModifiedBy { get; set; }
}