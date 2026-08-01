using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Collects Custom HTML source while the owner retains validation authority.</summary>
public partial class CustomHtmlFragmentEditorDialog
{
    private ExpandableMonacoSourceEditor? _editor;
    private ElementReference _visualEditorElement;
    private TiptapMarkdownEditorInterop? _visualEditor;
    private DotNetObjectReference<CustomHtmlFragmentEditorDialog>? _callbackReference;
    private string _source = string.Empty;
    private string? _localError;
    private bool _parametersInitialized;
    private bool _isExpanded;
    private bool _showCode;
    private bool _isVisualReady;
    private bool _isSwitchingMode;
    private bool _visualDirty;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>Gets or sets the source retained by the selected fragment.</summary>
    [Parameter, EditorRequired]
    public string InitialSource { get; set; } = string.Empty;

    /// <summary>Gets or sets the owner-provided validation error.</summary>
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

    protected string? EffectiveError => _localError ?? ErrorMessage;

    protected string DialogCssClass => _isExpanded
        ? "aero-custom-html-dialog aero-custom-html-dialog--expanded"
        : "aero-custom-html-dialog";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_parametersInitialized)
        {
            return;
        }

        _source = InitialSource;
        _parametersInitialized = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_showCode || _visualEditor is not null)
        {
            return;
        }

        try
        {
            _callbackReference ??= DotNetObjectReference.Create(this);
            _visualEditor = new TiptapMarkdownEditorInterop(JS);
            await _visualEditor.InitializeAsync(_visualEditorElement, _source, _callbackReference);
            _isVisualReady = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (JSException exception)
        {
            _localError = $"The visual HTML editor could not be loaded: {exception.Message}";
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task ToggleModeAsync()
    {
        if (_isSwitchingMode)
        {
            return;
        }

        _isSwitchingMode = true;
        _localError = null;
        try
        {
            if (_showCode)
            {
                _source = _editor is null ? _source : await _editor.GetValueAsync();
                _editor = null;
                _showCode = false;
                _isExpanded = false;
                _visualDirty = false;
                return;
            }

            if (_visualEditor is not null && _isVisualReady && _visualDirty)
            {
                _source = await _visualEditor.GetHtmlAsync();
            }

            await DisposeVisualEditorAsync();
            _showCode = true;
        }
        catch (JSException exception)
        {
            _localError = $"The editor view could not be changed: {exception.Message}";
        }
        finally
        {
            _isSwitchingMode = false;
        }
    }

    protected async Task ExecuteAsync(string command)
    {
        if (_visualEditor is null || !_isVisualReady)
        {
            return;
        }

        try
        {
            _localError = null;
            await _visualEditor.ExecuteAsync(command);
        }
        catch (JSException exception)
        {
            _localError = $"The HTML formatting command failed: {exception.Message}";
        }
    }

    protected async Task SaveAsync()
    {
        var source = _source;
        if (_showCode && _editor is not null)
        {
            source = await _editor.GetValueAsync();
        }
        else if (!_showCode && _visualEditor is not null && _isVisualReady && _visualDirty)
        {
            source = await _visualEditor.GetHtmlAsync();
        }

        await SourceSaved.InvokeAsync(source);
    }

    protected Task RequestAiAsync() =>
        AiEnabled ? AiRequested.InvokeAsync() : Task.CompletedTask;

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task SetExpandedAsync(bool isExpanded)
    {
        _isExpanded = isExpanded;
        return Task.CompletedTask;
    }

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    /// <summary>Marks the visual representation as authoritative after a user edit.</summary>
    [JSInvokable]
    public Task OnTiptapContentChanged()
    {
        _visualDirty = true;
        return Task.CompletedTask;
    }

    /// <summary>Accepts formatting-state notifications used by the shared Tiptap bridge.</summary>
    [JSInvokable]
    public Task OnTiptapFormattingStateChanged(JsonElement _)
        => Task.CompletedTask;

    private async ValueTask DisposeVisualEditorAsync()
    {
        _isVisualReady = false;
        if (_visualEditor is not null)
        {
            await _visualEditor.DisposeAsync();
            _visualEditor = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeVisualEditorAsync();
        _callbackReference?.Dispose();
    }
}
