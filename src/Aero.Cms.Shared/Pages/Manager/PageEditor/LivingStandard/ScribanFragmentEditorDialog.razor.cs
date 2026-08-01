using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Aero.Cms.Abstractions.Pages.Composition;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Authors a PageEditor Scriban fragment with the existing Monaco component.</summary>
public partial class ScribanFragmentEditorDialog
{
    private ExpandableMonacoSourceEditor? _editor;
    private bool _isExpanded;
    private bool _isSaving;

    /// <summary>Gets or sets the stable fragment node identifier used for the editor DOM ID.</summary>
    [Parameter, EditorRequired]
    public long NodeId { get; set; }

    /// <summary>Gets or sets the source strategy being edited.</summary>
    [Parameter, EditorRequired]
    public PageRenderedFragmentKind Kind { get; set; } = PageRenderedFragmentKind.Scriban;

    /// <summary>Gets or sets the Monaco language identifier.</summary>
    [Parameter, EditorRequired]
    public string Language { get; set; } = "liquid";

    /// <summary>Gets or sets the initial source.</summary>
    [Parameter, EditorRequired]
    public string InitialSource { get; set; } = string.Empty;

    /// <summary>Gets or sets the server validation error.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the save callback.</summary>
    [Parameter]
    public EventCallback<string> SourceSaved { get; set; }

    /// <summary>Gets or sets the close callback.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Gets or sets whether an enabled AI provider is available.</summary>
    [Parameter]
    public bool AiEnabled { get; set; }

    /// <summary>Gets or sets the disabled-state explanation for the AI action.</summary>
    [Parameter]
    public string AiUnavailableMessage { get; set; }
        = "Configure and enable an AI provider to use AI assistance.";

    /// <summary>Gets or sets the callback that opens the manager AI assistant.</summary>
    [Parameter]
    public EventCallback AiRequested { get; set; }

    protected string AiButtonTitle => AiEnabled ? "Open AI assistant" : AiUnavailableMessage;

    protected string EditorId => $"page-source-fragment-{NodeId}";

    protected string TitleId => $"aero-source-fragment-title-{NodeId}";

    protected string DisplayName => Kind switch
    {
        PageRenderedFragmentKind.SharpTs => "TS",
        PageRenderedFragmentKind.Htmx => "HTMX",
        _ => "Scriban template"
    };

    protected string ContextHelp => Kind switch
    {
        PageRenderedFragmentKind.SharpTs =>
            "The <code>render(context)</code> function receives <code>page</code>, <code>site</code>, <code>content</code>, and <code>isPreview</code>.",
        PageRenderedFragmentKind.Htmx =>
            "Use validated same-origin HTMX attributes such as <code>hx-get</code>, <code>hx-target</code>, and <code>hx-swap</code>.",
        _ =>
            "Available scopes: <code>page.id</code>, <code>page.title</code>, <code>page.slug</code>, <code>page.path</code>, <code>page.culture</code>, and <code>site.id</code>."
    };

    protected string RuntimeHelp => Kind switch
    {
        PageRenderedFragmentKind.SharpTs =>
            "Validation and rendering run on the server through the experimental interpret-only SharpTS runtime.",
        PageRenderedFragmentKind.Htmx =>
            "HTMX markup is validated and rendered through Aero's bounded HTML policy.",
        _ =>
            "Validation and rendering run on the server through the bounded Scriban runtime."
    };

    protected string DialogCssClass => _isExpanded
        ? "aero-scriban-fragment-dialog aero-scriban-fragment-dialog--expanded"
        : "aero-scriban-fragment-dialog";

    protected async Task SaveAsync()
    {
        _isSaving = true;
        try
        {
            var source = _editor is null
                ? InitialSource
                : await _editor.GetValueAsync();

            await SourceSaved.InvokeAsync(source);
        }
        finally
        {
            _isSaving = false;
        }
    }

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task RequestAiAsync() =>
        AiEnabled ? AiRequested.InvokeAsync() : Task.CompletedTask;

    protected Task SetExpandedAsync(bool isExpanded)
    {
        _isExpanded = isExpanded;
        return Task.CompletedTask;
    }

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;
}
