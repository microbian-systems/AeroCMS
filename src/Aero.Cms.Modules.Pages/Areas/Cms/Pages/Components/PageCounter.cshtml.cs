using Hydro;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages.Components;

// ~/Pages/Components/PageCounter.cshtml.cs

/// <summary>
/// Represents a class for PageCounter.
/// </summary>
public class PageCounter : HydroComponent
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