using Aero.Cms.Abstractions.Interfaces;
using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

[GenerateSerializer]
[Alias("AeroEntityViewModel")]
public abstract record AeroEntityViewModel : IEntity
{
    [Id(1000)]
    public long Id { get; set; }
    [Id(1001)]
    public long SiteId { get; set; } // todo - should the site id be passe back down to clients ?
    [Id(1002)]
    public DateTimeOffset CreatedOn { get; set; }
    [Id(1003)]
    public DateTimeOffset? ModifiedOn { get; set; }
    [Id(1004)]
    public string CreatedBy { get; set; } = null!;
    [Id(1005)]
    public string ModifiedBy { get; set; } = null!;
    [Id(1006)]
    public Dictionary<string, object> MetaData { get; } = [];
}

