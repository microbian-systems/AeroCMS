using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Banner;

/// <summary>
/// Persisted banner content and its display-window metadata.
/// </summary>
/// <remarks>
/// The model itself does not render <see cref="Message"/>, evaluate the schedule, scope the banner to a
/// site or culture, or implement client-side dismissal. Those decisions must be made by a consumer.
/// </remarks>
public class BannerModel : SableDocument, IAuditable
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
    /// <summary>Gets or sets the banner message. This model does not sanitize or render it.</summary>
public string Message { get; set; }
    /// <summary>Gets or sets the optional configured start of the banner's display window.</summary>
public DateTimeOffset? StartDate { get; set; }
    /// <summary>Gets or sets the optional configured end of the banner's display window.</summary>
public DateTimeOffset? EndDate { get; set; }
    /// <summary>Gets or sets whether a renderer should suppress a close affordance.</summary>
    public bool DisableClose { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}
