using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Modules.Commerce.Client.Pages.Manager;

public partial class Subscriptions : ComponentBase, IDisposable
{
    private const int PageSize = 20;
    private readonly CancellationTokenSource cancellation = new();
    private bool initialized;
    [Inject] protected ICommerceManagerClient Client { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    protected IReadOnlyList<ManagerSubscriptionSummaryDto> Rows { get; private set; } = [];
    protected string? ErrorMessage { get; private set; }
    protected long TotalCount { get; private set; }
    protected int Skip { get; private set; }
    protected bool IsLoading { get; private set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || initialized) return;
        initialized = true;
        await LoadAsync();
        StateHasChanged();
    }

    protected void View(long id) => Navigation.NavigateTo($"/manager/commerce/subscriptions/{id}");
    protected async Task PreviousPageAsync() { Skip = Math.Max(0, Skip - PageSize); await LoadAsync(); }
    protected async Task NextPageAsync() { if (Skip + Rows.Count < TotalCount) { Skip += PageSize; await LoadAsync(); } }

    private async Task LoadAsync()
    {
        IsLoading = true; ErrorMessage = null;
        try
        {
            var result = await Client.GetSubscriptionsAsync(Skip, PageSize, cancellation.Token);
            if (result is Result<ManagerSubscriptionPage<ManagerSubscriptionSummaryDto>, AeroError>.Ok ok) { Rows = ok.Value.Items; TotalCount = ok.Value.TotalCount; }
            else if (result is Result<ManagerSubscriptionPage<ManagerSubscriptionSummaryDto>, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
        }
        finally { IsLoading = false; }
    }

    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
}
