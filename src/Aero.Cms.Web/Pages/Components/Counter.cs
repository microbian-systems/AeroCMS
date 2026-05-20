using Hydro;

namespace Aero.Cms.Web.Pages.Components;

public partial class Counter : HydroComponent
{
    public int Count { get; set; }

    public void Add()
    {
        Count++;
    }
}
