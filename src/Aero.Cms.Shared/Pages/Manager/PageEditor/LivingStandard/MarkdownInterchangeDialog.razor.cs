using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Hosts the Markdown import and export presentation used by the HTML-first page editor.
/// </summary>
/// <remarks>
/// Import conversion and validation are intentionally delegated to the owning editor. The
/// initial text is captured once so ordinary parent renders do not overwrite user edits.
/// </remarks>
public partial class MarkdownInterchangeDialog
{
    private string _markdown = string.Empty;
    private bool _initialized;

    /// <summary>
    /// Gets or sets whether the dialog collects an import or presents an export.
    /// </summary>
    [Parameter]
    public MarkdownInterchangeMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the text used to initialize the editor on its first parameter pass.
    /// </summary>
    /// <remarks>Subsequent parameter updates do not replace the current editor contents.</remarks>
    [Parameter]
    public string InitialValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an interchange error to display without closing the dialog.
    /// </summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives the current Markdown import text.
    /// </summary>
    [Parameter]
    public EventCallback<string> ImportRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests dismissal of the dialog.
    /// </summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    private string Title => Mode == MarkdownInterchangeMode.Import
        ? "Import page content"
        : "Export page content";

    private string Description => Mode == MarkdownInterchangeMode.Import
        ? "Raw HTML is treated as text. Generated HTML must pass the element, attribute, URL, nesting, and size policies before it is added as one undoable action."
        : "Only semantic content that Markdown can preserve losslessly is exported. Styled layouts and unsupported HTML remain in the page and cause an explicit export error.";

    private string Placeholder => Mode == MarkdownInterchangeMode.Import
        ? "# Heading\n\nWrite or paste Markdown here."
        : string.Empty;

    /// <summary>
    /// Captures <see cref="InitialValue"/> once while preserving any later user edits.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (_initialized)
        {
            return;
        }

        _markdown = InitialValue;
        _initialized = true;
    }

    /// <summary>
    /// Forwards the current Markdown text to the owning editor for conversion and validation.
    /// </summary>
    /// <returns>A task that completes when the import callback has finished.</returns>
    private Task ImportAsync() => ImportRequested.InvokeAsync(_markdown);

    /// <summary>
    /// Requests that the owning editor close the dialog.
    /// </summary>
    /// <returns>A task that completes when the close callback has finished.</returns>
    private Task CloseAsync() => Closed.InvokeAsync();
}
