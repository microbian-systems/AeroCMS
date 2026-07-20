using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Collects an HTML fragment and returns it to the owning editor for policy validation and
/// insertion.
/// </summary>
/// <remarks>
/// The dialog does not parse or trust the fragment itself. The callback recipient remains
/// responsible for applying the page editor's element, attribute, URL, nesting, and size
/// policies.
/// </remarks>
public partial class HtmlFragmentImportDialog
{
    private string _fragment = string.Empty;

    /// <summary>
    /// Gets or sets the validation or import error to display without closing the dialog.
    /// </summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives the fragment exactly as entered.
    /// </summary>
    [Parameter]
    public EventCallback<string> ImportRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests dismissal without importing.
    /// </summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>
    /// Forwards the current fragment to the owning editor without performing local validation.
    /// </summary>
    /// <returns>A task that completes when the import callback has finished.</returns>
    private Task ImportAsync() => ImportRequested.InvokeAsync(_fragment);

    /// <summary>
    /// Requests that the owning editor close the dialog.
    /// </summary>
    /// <returns>A task that completes when the close callback has finished.</returns>
    private Task CloseAsync() => Closed.InvokeAsync();
}
