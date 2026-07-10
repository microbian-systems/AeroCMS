using Aero.Core.Entities;

namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for AeroEntityViewModel.
/// </summary>
[GenerateSerializer]
[Alias("AeroEntityViewModel")]
public abstract record AeroEntityViewModel : IEntity
{
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
[Id(1000)]
    public long Id { get; set; }
        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
[Id(1001)]
    public long SiteId { get; set; } // todo - should the site id be passe back down to clients ?
        /// <summary>
    /// Gets or sets the Created On.
    /// </summary>
[Id(1002)]
    public DateTimeOffset CreatedOn { get; set; }
        /// <summary>
    /// Gets or sets the Modified On.
    /// </summary>
[Id(1003)]
    public DateTimeOffset? ModifiedOn { get; set; }
        /// <summary>
    /// Gets or sets the Created By.
    /// </summary>
[Id(1004)]
    public string CreatedBy { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Modified By.
    /// </summary>
[Id(1005)]
    public string ModifiedBy { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Meta Data.
    /// </summary>
[Id(1006)]
    public Dictionary<string, object> MetaData { get; } = [];
}

