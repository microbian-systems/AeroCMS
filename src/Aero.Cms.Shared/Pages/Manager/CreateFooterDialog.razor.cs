using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

public sealed record CreateFooterDialogResult(string Name, string? Description);

public partial class CreateFooterDialog
{
    [Inject] private DialogService DialogService { get; set; } = default!;

    private string _name = "Site Footer";
    private string? _description = "Primary site footer";

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            return;
        }

        DialogService.Close(new CreateFooterDialogResult(_name.Trim(), _description?.Trim()));
    }

    private void Cancel()
    {
        DialogService.Close(null);
    }
}
