using Aero.Core.Entities;

namespace Aero.Cms.Modules.Banner;

/// <summary>
/// Represents a class for BannerModel.
/// </summary>
public class BannerModel : Entity
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public string? Name { get; set; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description { get; set; }
        /// <summary>
    /// Gets or sets the Message.
    /// </summary>
public string Message { get; set; }
        /// <summary>
    /// Gets or sets the Start Date.
    /// </summary>
public DateTimeOffset? StartDate { get; set; }
        /// <summary>
    /// Gets or sets the End Date.
    /// </summary>
public DateTimeOffset? EndDate { get; set; }
        /// <summary>
    /// Gets or sets the Disable Close.
    /// </summary>
public bool DisableClose { get; set; }
}