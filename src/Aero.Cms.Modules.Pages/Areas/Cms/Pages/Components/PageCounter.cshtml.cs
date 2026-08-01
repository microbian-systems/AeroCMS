using Hydro;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages.Components;

// ~/Pages/Components/PageCounter.cshtml.cs

/// <summary>
/// Provides the mutable counter state for the Hydro page-counter component.
/// </summary>
public class PageCounter : HydroComponent
{
    /// <summary>
    /// Gets or sets the current counter value.
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
