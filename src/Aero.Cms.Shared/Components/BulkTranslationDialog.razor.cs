using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Components;

/// <summary>
/// Represents a record for BulkTranslationDialogResult.
/// </summary>
public sealed record BulkTranslationDialogResult(bool OverwriteExisting);

/// <summary>
/// Represents a class for BulkTranslationDialog.
/// </summary>
public partial class BulkTranslationDialog
{
    [Inject] private DialogService DialogService { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Missing Cultures.
    /// </summary>
[Parameter] public IReadOnlyList<string> MissingCultures { get; set; } = [];
        /// <summary>
    /// Gets or sets the Existing Cultures.
    /// </summary>
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
