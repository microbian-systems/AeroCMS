using Hydro;

namespace Aero.Cms.Web.Pages.Components;

/// <summary>
/// Represents a class for Counter.
/// </summary>
public partial class Counter : HydroComponent
{
        /// <summary>
    /// Gets or sets the Count.
    /// </summary>
public int Count { get; set; }

        /// <summary>
    /// Add method.
    /// </summary>
public void Add()
    {
        Count++;
    }
}
