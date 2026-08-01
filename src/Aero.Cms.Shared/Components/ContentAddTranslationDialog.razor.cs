using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Components;

/// <summary>
/// Represents a record for ContentAddTranslationDialogResult.
/// </summary>
public sealed record ContentAddTranslationDialogResult(string Culture, string Slug);

/// <summary>
/// Represents a class for ContentAddTranslationDialog.
/// </summary>
public partial class ContentAddTranslationDialog
{
    [Inject] private DialogService DialogService { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Available Cultures.
    /// </summary>
[Parameter] public IReadOnlyList<string> AvailableCultures { get; set; } = [];
        /// <summary>
    /// Gets or sets the Source Slug.
    /// </summary>
[Parameter] public string SourceSlug { get; set; } = string.Empty;

    private string _culture = string.Empty;
    private string _slug = string.Empty;

    private bool CanSubmit =>
        !string.IsNullOrWhiteSpace(_culture) &&
        !string.IsNullOrWhiteSpace(_slug);

        /// <summary>
    /// OnParametersSet method.
    /// </summary>
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
