using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class MarkdownInterchangeDialog
{
    private string _markdown = string.Empty;
    private bool _initialized;

    [Parameter]
    public MarkdownInterchangeMode Mode { get; set; }

    [Parameter]
    public string InitialValue { get; set; } = string.Empty;

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback<string> ImportRequested { get; set; }

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

    protected override void OnParametersSet()
    {
        if (_initialized)
        {
            return;
        }

        _markdown = InitialValue;
        _initialized = true;
    }

    private Task ImportAsync() => ImportRequested.InvokeAsync(_markdown);

    private Task CloseAsync() => Closed.InvokeAsync();
}
