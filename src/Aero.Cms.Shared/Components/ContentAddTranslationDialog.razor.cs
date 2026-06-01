using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Components;

public sealed record ContentAddTranslationDialogResult(string Culture, string Slug);

public partial class ContentAddTranslationDialog
{
    [Inject] private DialogService DialogService { get; set; } = default!;

    [Parameter] public IReadOnlyList<string> AvailableCultures { get; set; } = [];
    [Parameter] public string SourceSlug { get; set; } = string.Empty;

    private string _culture = string.Empty;
    private string _slug = string.Empty;

    private bool CanSubmit =>
        !string.IsNullOrWhiteSpace(_culture) &&
        !string.IsNullOrWhiteSpace(_slug);

    protected override void OnParametersSet()
    {
        _culture = AvailableCultures.FirstOrDefault() ?? string.Empty;
        _slug = SourceSlug;
    }

    private void Submit()
    {
        if (!CanSubmit)
        {
            return;
        }

        DialogService.Close(new ContentAddTranslationDialogResult(_culture.Trim(), _slug.Trim()));
    }

    private void Cancel()
    {
        DialogService.Close(null);
    }
}
