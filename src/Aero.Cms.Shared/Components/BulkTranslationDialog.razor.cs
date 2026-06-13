using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Components;

public sealed record BulkTranslationDialogResult(bool OverwriteExisting);

public partial class BulkTranslationDialog
{
    [Inject] private DialogService DialogService { get; set; } = default!;

    [Parameter] public IReadOnlyList<string> MissingCultures { get; set; } = [];
    [Parameter] public IReadOnlyList<string> ExistingCultures { get; set; } = [];

    private bool _overwriteExisting;

    private void Submit()
    {
        DialogService.Close(new BulkTranslationDialogResult(_overwriteExisting));
    }

    private void Cancel()
    {
        DialogService.Close(null);
    }
}
