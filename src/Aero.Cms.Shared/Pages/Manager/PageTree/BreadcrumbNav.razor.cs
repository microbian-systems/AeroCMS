using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageTree;

/// <summary>
/// Breadcrumb navigation using the Pages HTTP client.
/// Shows the current page's position in the site hierarchy.
/// </summary>
public partial class BreadcrumbNav
{
    [Inject] private IPagesHttpClient PagesClient { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    /// <summary>
    /// The ID of the page to show breadcrumbs for.
    /// </summary>
    [Parameter]
    public long PageId { get; set; }

    private IReadOnlyList<TreeBreadcrumbItem> _items = [];

        /// <summary>
    /// OnParametersSetAsync method.
    /// </summary>
protected override async Task OnParametersSetAsync()
    {
        if (PageId <= 0) return;

        try
        {
            var result = await PagesClient.GetBreadcrumbAsync(PageId);
            if (result is Result<IReadOnlyList<TreeBreadcrumbItem>, AeroError>.Ok ok)
            {
                _items = ok.Value;
            }
        }
        catch
        {
            _items = [];
        }
    }

    private void NavigateTo(TreeBreadcrumbItem item)
    {
        NavigationManager.NavigateTo($"/_cms/manager/pages/{item.Id}");
    }
}
