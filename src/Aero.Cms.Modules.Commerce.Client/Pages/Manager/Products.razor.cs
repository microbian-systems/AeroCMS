using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Modules.Commerce.Client.Pages.Manager;

public partial class Products : ComponentBase, IDisposable
{
    private const int PageSize = 20;
    private readonly CancellationTokenSource cancellation = new();
    private bool initialized;

    [Inject] protected ICommerceManagerClient Client { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected DialogService Dialogs { get; set; } = null!;
    [Inject] protected NotificationService Notifications { get; set; } = null!;

    protected IReadOnlyList<ManagerProductDto> ProductRows { get; private set; } = [];
    protected string? SearchText { get; set; }
    protected string? ErrorMessage { get; private set; }
    protected long TotalCount { get; private set; }
    protected int Skip { get; private set; }
    protected bool IsLoading { get; private set; }
    protected bool CanLoadNext => Skip + ProductRows.Count < TotalCount;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || initialized) return;
        initialized = true;
        await LoadAsync();
        StateHasChanged();
    }

    protected void CreateProduct() => Navigation.NavigateTo("/manager/commerce/products/new");
    protected void EditProduct(long id) => Navigation.NavigateTo($"/manager/commerce/products/{id}");
    protected async Task SearchAsync() { Skip = 0; await LoadAsync(); }
    protected async Task PreviousPageAsync() { Skip = Math.Max(0, Skip - PageSize); await LoadAsync(); }
    protected async Task NextPageAsync() { if (CanLoadNext) { Skip += PageSize; await LoadAsync(); } }

    protected async Task DeleteProductAsync(ManagerProductDto product)
    {
        if (await Dialogs.Confirm($"Delete '{product.Name}'? Products used by a listing cannot be deleted.", "Delete product") != true) return;
        var result = await Client.DeleteProductAsync(product.Id, cancellation.Token);
        if (result is Result<bool, AeroError>.Ok)
        {
            Notifications.Notify(NotificationSeverity.Success, "Product deleted");
            await LoadAsync();
        }
        else if (result is Result<bool, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await Client.GetProductsAsync(SearchText, Skip, PageSize, cancellation.Token);
            if (result is Result<ManagerCatalogPage<ManagerProductDto>, AeroError>.Ok ok)
            {
                ProductRows = ok.Value.Items;
                TotalCount = ok.Value.TotalCount;
            }
            else if (result is Result<ManagerCatalogPage<ManagerProductDto>, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
        }
        finally { IsLoading = false; }
    }

    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
}
