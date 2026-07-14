using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlRichTextEditorDialog
{
    private readonly TiptapInlineContentConverter _converter = new();
    private ElementReference _editorElement;
    private ElementReference _closeButton;
    private TiptapEditorInterop? _interop;
    private string? _initialHtml;
    private string? _localError;
    private string? _linkUrl;
    private bool _isReady;
    private bool _isSaving;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = null!;

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<HtmlNode>> ContentSaved { get; set; }

    protected string DisplayName => Node.TagName is { Length: > 0 } tag ? $"<{tag}>" : "text";
    protected string? EffectiveError => _localError ?? ErrorMessage;

    protected override void OnParametersSet()
    {
        if (_initialHtml is not null)
        {
            return;
        }

        var initial = _converter.ToEditorHtml(Node);
        switch (initial)
        {
            case Result<string>.Ok ok:
                _initialHtml = ok.Value;
                break;
            case Result<string>.Failure failure:
                _localError = FormatError(failure.Error);
                break;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialHtml is null || _localError is not null)
        {
            return;
        }

        try
        {
            _interop = new TiptapEditorInterop(JS);
            await _interop.InitializeAsync(_editorElement, _initialHtml);
            _isReady = true;
            await _closeButton.FocusAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (JSException exception)
        {
            _localError = $"The browser text editor could not be loaded: {exception.Message}";
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
            _localError = $"The formatting command failed: {exception.Message}";
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
            var json = await _interop.GetDocumentJsonAsync();
            var converted = _converter.FromDocumentJson(json);
            switch (converted)
            {
                case Result<IReadOnlyList<HtmlNode>>.Ok ok:
                    await ContentSaved.InvokeAsync(ok.Value);
                    break;
                case Result<IReadOnlyList<HtmlNode>>.Failure failure:
                    _localError = FormatError(failure.Error);
                    break;
            }
        }
        catch (JSException exception)
        {
            _localError = $"The edited text could not be read: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape"
        ? CloseAsync()
        : Task.CompletedTask;

    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        AeroError.NotAllowed notAllowed => notAllowed.msg,
        AeroError.NotFound notFound => notFound.msg,
        AeroError.Conflict conflict => conflict.msg,
        AeroError.Error general => general.msg,
        _ => error.ToString()
    };

    public async ValueTask DisposeAsync()
    {
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }
    }
}
