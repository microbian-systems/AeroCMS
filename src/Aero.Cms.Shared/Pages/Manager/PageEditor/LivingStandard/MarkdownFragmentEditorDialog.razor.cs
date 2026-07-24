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
    private ExpandableMonacoSourceEditor? _sourceEditor;
    private TiptapMarkdownEditorInterop? _interop;
    private string _source = string.Empty;
    private string _visualHtml = string.Empty;
    private string? _localError;
    private string? _linkUrl;
    private bool _parametersInitialized;
    private bool _showCode;
    private bool _isExpanded;
    private bool _isSwitchingMode;
    private bool _isReady;
    private bool _isSaving;
    private bool _focusCloseButton = true;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>Gets or sets the validated HTML produced from the persisted Markdown source.</summary>
    [Parameter, EditorRequired]
    public string InitialHtml { get; set; } = string.Empty;

    /// <summary>Gets or sets the persisted Markdown source shown by Monaco.</summary>
    [Parameter, EditorRequired]
    public string InitialSource { get; set; } = string.Empty;

    /// <summary>Gets or sets the owner error shown when no local interop error exists.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the close callback.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Gets or sets the callback receiving edited HTML for authoritative conversion.</summary>
    [Parameter]
    public EventCallback<string> HtmlSaved { get; set; }

    /// <summary>Gets or sets the callback receiving validated Markdown source.</summary>
    [Parameter]
    public EventCallback<string> SourceSaved { get; set; }

    /// <summary>Converts edited visual HTML to validated Markdown for view switching.</summary>
    [Parameter, EditorRequired]
    public Func<string, FragmentSourceConversionResult> HtmlToMarkdown { get; set; } = null!;

    /// <summary>Converts edited Markdown to validated HTML for view switching.</summary>
    [Parameter, EditorRequired]
    public Func<string, FragmentSourceConversionResult> MarkdownToHtml { get; set; } = null!;

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

    protected string DialogCssClass => _isExpanded
        ? "aero-markdown-fragment-dialog aero-markdown-fragment-dialog--expanded"
        : "aero-markdown-fragment-dialog";

    protected string AiButtonTitle => AiEnabled ? "Open AI assistant" : AiUnavailableMessage;

    protected string? EffectiveError => _localError ?? ErrorMessage;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_parametersInitialized)
        {
            return;
        }

        _source = InitialSource;
        _visualHtml = InitialHtml;
        _parametersInitialized = true;
    }

    /// <summary>Initializes Tiptap whenever the visual view becomes active.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_showCode || _interop is not null)
        {
            return;
        }

        try
        {
            _interop = new TiptapMarkdownEditorInterop(JS);
            await _interop.InitializeAsync(_editorElement, _visualHtml);
            _isReady = true;
            if (_focusCloseButton)
            {
                _focusCloseButton = false;
                await _closeButton.FocusAsync();
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (JSException exception)
        {
            _localError = $"The browser Markdown editor could not be loaded: {exception.Message}";
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
                var source = _sourceEditor is null
                    ? _source
                    : await _sourceEditor.GetValueAsync();
                var conversion = MarkdownToHtml(source);
                if (!conversion.Succeeded)
                {
                    _localError = conversion.ErrorMessage;
                    return;
                }

                _source = source;
                _visualHtml = conversion.Value;
                _sourceEditor = null;
                _showCode = false;
                _isExpanded = false;
                return;
            }

            if (_interop is null || !_isReady)
            {
                return;
            }

            var html = await _interop.GetHtmlAsync();
            var conversionToSource = HtmlToMarkdown(html);
            if (!conversionToSource.Succeeded)
            {
                _localError = conversionToSource.ErrorMessage;
                return;
            }

            _source = conversionToSource.Value;
            _visualHtml = html;
            await DisposeVisualEditorAsync();
            _showCode = true;
        }
        catch (JSException exception)
        {
            _localError = $"The Markdown editor view could not be changed: {exception.Message}";
        }
        finally
        {
            _isSwitchingMode = false;
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
        if (_showCode)
        {
            _isSaving = true;
            _localError = null;
            try
            {
                var source = _sourceEditor is null
                    ? _source
                    : await _sourceEditor.GetValueAsync();
                await SourceSaved.InvokeAsync(source);
            }
            catch (JSException exception)
            {
                _localError = $"The edited Markdown source could not be read: {exception.Message}";
            }
            finally
            {
                _isSaving = false;
            }
            return;
        }

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

    protected Task RequestAiAsync() =>
        AiEnabled ? AiRequested.InvokeAsync() : Task.CompletedTask;

    protected Task SetExpandedAsync(bool isExpanded)
    {
        _isExpanded = isExpanded;
        return Task.CompletedTask;
    }

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeVisualEditorAsync();
    }

    private async ValueTask DisposeVisualEditorAsync()
    {
        _isReady = false;
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
            _interop = null;
        }
    }
}

/// <summary>Represents one synchronous Markdown/HTML editor-view conversion.</summary>
public sealed record FragmentSourceConversionResult(
    bool Succeeded,
    string Value,
    string? ErrorMessage)
{
    /// <summary>Creates a successful conversion.</summary>
    public static FragmentSourceConversionResult Success(string value) => new(true, value, null);

    /// <summary>Creates a failed conversion.</summary>
    public static FragmentSourceConversionResult Failure(string errorMessage) => new(false, string.Empty, errorMessage);
}
