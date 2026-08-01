using Aero.Cms.Modules.Commerce.Client.Models;
using Aero.Cms.Modules.Commerce.Client.Services;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;

namespace Aero.Cms.Modules.Commerce.Client.Pages.Manager;

public partial class ProductEditor : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly IValidator<ProductEditorModel> validator = new ProductEditorModelValidator();
    private bool initialized;

    [Parameter] public long Id { get; set; }
    [Inject] protected ICommerceManagerClient Client { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected NotificationService Notifications { get; set; } = null!;

    protected bool IsNew => Id == 0;
    protected ProductEditorModel Model { get; private set; } = new();
    protected IReadOnlyList<string> ValidationErrors { get; private set; } = [];
    protected string? ErrorMessage { get; private set; }
    protected bool IsLoading { get; private set; }
    protected bool IsSaving { get; private set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive || initialized) return;
        initialized = true;
        if (!IsNew) await LoadAsync();
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
                ? await Client.CreateProductAsync(Model.ToRequest(), cancellation.Token)
                : await Client.UpdateProductAsync(Id, Model.ToRequest(), cancellation.Token);
            if (result is Result<ManagerProductDto, AeroError>.Ok)
            {
                Notifications.Notify(NotificationSeverity.Success, IsNew ? "Product created" : "Product saved");
                Navigation.NavigateTo("/manager/commerce/products");
            }
            else if (result is Result<ManagerProductDto, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
        }
        finally { IsSaving = false; }
    }

    protected void Cancel() => Navigation.NavigateTo("/manager/commerce/products");

    private async Task LoadAsync()
    {
        IsLoading = true;
        var result = await Client.GetProductAsync(Id, cancellation.Token);
        if (result is Result<ManagerProductDto, AeroError>.Ok ok) Model = ProductEditorModel.From(ok.Value);
        else if (result is Result<ManagerProductDto, AeroError>.Failure failure) ErrorMessage = failure.Error.ToString();
        IsLoading = false;
    }

    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
}
