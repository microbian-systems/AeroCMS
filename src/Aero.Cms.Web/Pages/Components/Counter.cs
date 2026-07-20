using Hydro;

namespace Aero.Cms.Web.Pages.Components;

/// <summary>
/// Demonstrates a stateful Hydro counter component.
/// </summary>
public partial class Counter : HydroComponent
{
    /// <summary>
    /// Gets or sets the component's current count.
    /// </summary>
public int Count { get; set; }

    /// <summary>
    /// Increments <see cref="Count"/> by one.
    /// </summary>
public void Add()
    {
        Count++;
    }
}
