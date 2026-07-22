using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Authors a Markdown fragment through block-capable Tiptap and returns HTML to
/// the owner for validated HTML import and lossless Markdown export.
/// </summary>
public partial class MarkdownFragmentEditorDialog
{
    private ElementReference _editorElement;
    private ElementReference _closeButton;
    private TiptapMarkdownEditorInterop? _interop;
    private string? _localError;
    private string? _linkUrl;
    private bool _isReady;
    private bool _isSaving;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>Gets or sets the validated HTML produced from the persisted Markdown source.</summary>
    [Parameter, EditorRequired]
    public string InitialHtml { get; set; } = string.Empty;

    /// <summary>Gets or sets the owner error shown when no local interop error exists.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the close callback.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Gets or sets the callback receiving edited HTML for authoritative conversion.</summary>
    [Parameter]
    public EventCallback<string> HtmlSaved { get; set; }

    protected string? EffectiveError => _localError ?? ErrorMessage;

    /// <summary>Initializes Tiptap after the dialog first renders.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            _interop = new TiptapMarkdownEditorInterop(JS);
            await _interop.InitializeAsync(_editorElement, InitialHtml);
            _isReady = true;
            await _closeButton.FocusAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (JSException exception)
        {
            _localError = $"The browser Markdown editor could not be loaded: {exception.Message}";
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task ExecuteAsync(string command, string? argument = null)
    {
        if (_interop is null || !_isReady)
        {
            return;
        }

        try
        {
            _localError = null;
            await _interop.ExecuteAsync(command, argument);
        }
        catch (JSException exception)
        {
            _localError = $"The Markdown formatting command failed: {exception.Message}";
        }
    }

    protected async Task SaveAsync()
    {
        if (_interop is null || !_isReady)
        {
            return;
        }

        _isSaving = true;
        _localError = null;
        try
        {
            await HtmlSaved.InvokeAsync(await _interop.GetHtmlAsync());
        }
        catch (JSException exception)
        {
            _localError = $"The edited Markdown content could not be read: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }
    }
}
