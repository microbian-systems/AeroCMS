using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

public abstract record EntityViewModel : IEntity
{
    public long Id { get; set; }
    public long SiteId { get; set; } // todo - should the site id be passe back down to clients ?
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? ModifiedOn { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string ModifiedBy { get; set; } = null!;
    public Dictionary<string, object> MetaData { get; } = [];
}