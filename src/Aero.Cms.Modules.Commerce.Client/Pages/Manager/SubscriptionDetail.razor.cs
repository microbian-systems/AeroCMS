using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Modules.Commerce.Client.Pages.Manager;

public partial class SubscriptionDetail : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private bool loaded;
    [Parameter] public long Id { get; set; }
    [Inject] protected ICommerceManagerClient Client { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    protected ManagerSubscriptionReceiptDto? Receipt { get; private set; }
    protected string? ErrorMessage { get; private set; }
    protected bool IsLoading { get; private set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || loaded) return;
        loaded = true; IsLoading = true;
        try
        {
            var result = await Client.GetSubscriptionAsync(Id, cancellation.Token);
            if (result is Result<ManagerSubscriptionReceiptDto, AeroError>.Ok ok) Receipt = ok.Value;
            else if (result is Result<ManagerSubscriptionReceiptDto, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
        }
        finally { IsLoading = false; StateHasChanged(); }
    }

    protected void Back() => Navigation.NavigateTo("/manager/commerce/subscriptions");
    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
}
