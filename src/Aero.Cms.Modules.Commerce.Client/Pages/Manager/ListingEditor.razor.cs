using Aero.Cms.Modules.Commerce.Client.Models;
using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;

namespace Aero.Cms.Modules.Commerce.Client.Pages.Manager;

public partial class ListingEditor : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly IValidator<ListingEditorModel> validator = new ListingEditorModelValidator();
    private ListingProductPicker? productPicker;
    private bool initialized;

    [Parameter] public long Id { get; set; }
    [Inject] protected ICommerceManagerClient Client { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected NotificationService Notifications { get; set; } = null!;

    protected bool IsNew => Id == 0;
    protected ListingEditorModel Model { get; private set; } = new();
    protected IReadOnlyList<ManagerProductDto> Products { get; private set; } = [];
    protected string? ProductSearch { get; set; }
    protected IReadOnlyList<string> ValidationErrors { get; private set; } = [];
    protected string? ErrorMessage { get; private set; }
    protected bool IsLoading { get; private set; }
    protected bool IsSaving { get; private set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || initialized) return;
        initialized = true;
        await LoadAsync();
        StateHasChanged();
    }

    protected async Task SaveAsync(EditContext _)
    {
        ValidationErrors = [];
        ErrorMessage = null;
        var validation = await validator.ValidateAsync(Model, cancellation.Token);
        if (!validation.IsValid) { ValidationErrors = validation.Errors.Select(error => error.ErrorMessage).Distinct().ToList(); return; }

        IsSaving = true;
        try
        {
            var result = IsNew
                ? await Client.CreateListingAsync(Model.ToRequest(), cancellation.Token)
                : await Client.UpdateListingAsync(Id, Model.ToRequest(), cancellation.Token);
            if (result is Result<ManagerListingDto, AeroError>.Ok)
            {
                Notifications.Notify(NotificationSeverity.Success, IsNew ? "Listing created" : "Listing saved");
                Navigation.NavigateTo("/manager/commerce/listings");
            }
            else if (result is Result<ManagerListingDto, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
        }
        finally { IsSaving = false; }
    }

    protected void Cancel() => Navigation.NavigateTo("/manager/commerce/listings");

    protected async Task SearchProductsAsync()
    {
        productPicker ??= new ListingProductPicker(Client);
        var result = await productPicker.SearchAsync(ProductSearch, Model.ProductId, cancellation.Token);
        if (result is Result<IReadOnlyList<ManagerProductDto>, AeroError>.Ok ok) Products = ok.Value;
        else if (result is Result<IReadOnlyList<ManagerProductDto>, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        if (!IsNew)
        {
            var listingResult = await Client.GetListingAsync(Id, cancellation.Token);
            if (listingResult is Result<ManagerListingDto, AeroError>.Ok listing) Model = ListingEditorModel.From(listing.Value);
            else if (listingResult is Result<ManagerListingDto, AeroError>.Failure listingFailure) ErrorMessage = listingFailure.Error.ToString();
        }
        await SearchProductsAsync();
        IsLoading = false;
    }

    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
}
