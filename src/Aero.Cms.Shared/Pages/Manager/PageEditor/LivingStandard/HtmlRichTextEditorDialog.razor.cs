using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Provides constrained rich-text editing for phrasing-content nodes through the Tiptap browser
/// integration.
/// </summary>
/// <remarks>
/// The dialog converts only the inline HTML subset supported by
/// <see cref="TiptapInlineContentConverter"/>. Saving returns converted child nodes to the owning
/// editor, which remains responsible for applying the page mutation.
/// </remarks>
public partial class HtmlRichTextEditorDialog
{
    private readonly TiptapInlineContentConverter _converter = new();
    private ElementReference _editorElement;
    private ElementReference _closeButton;
    private TiptapEditorInterop? _interop;
    private DotNetObjectReference<HtmlRichTextEditorDialog>? _callbackReference;
    private string? _initialHtml;
    private string? _localError;
    private string? _linkUrl;
    private bool _boldActive;
    private bool _italicActive;
    private bool _strikeActive;
    private bool _codeActive;
    private bool _isReady;
    private bool _isSaving;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>
    /// Gets or sets the selected HTML node whose inline children are being edited.
    /// </summary>
    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = null!;

    /// <summary>
    /// Gets or sets an owner-provided error to display when no local conversion or interop error
    /// is active.
    /// </summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests dismissal without saving.
    /// </summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives converted inline child nodes after a successful
    /// save.
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<HtmlNode>> ContentSaved { get; set; }

    /// <summary>
    /// Gets a compact element label for the dialog heading.
    /// </summary>
    protected string DisplayName => Node.TagName is { Length: > 0 } tag ? $"<{tag}>" : "text";

    /// <summary>
    /// Gets the local conversion or interop error, falling back to the owner-provided error.
    /// </summary>
    protected string? EffectiveError => _localError ?? ErrorMessage;

    /// <summary>
    /// Converts the selected node to initial editor HTML before browser initialization.
    /// </summary>
    /// <remarks>
    /// Once conversion succeeds, later parameter renders do not overwrite the editor's working
    /// document.
    /// </remarks>
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

    /// <summary>
    /// Creates the browser editor after the dialog first renders and initial conversion succeeds.
    /// </summary>
    /// <param name="firstRender">
    /// <see langword="true"/> for the first completed render; otherwise,
    /// <see langword="false"/>.
    /// </param>
    /// <returns>A task that completes after initialization or local error reporting.</returns>
    /// <remarks>
    /// JavaScript failures are converted to a user-facing local error rather than escaping the
    /// component lifecycle.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialHtml is null || _localError is not null)
        {
            return;
        }

        try
        {
            _interop = new TiptapEditorInterop(JS);
            _callbackReference = DotNetObjectReference.Create(this);
            await _interop.InitializeAsync(_editorElement, _initialHtml, _callbackReference);
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

    /// <summary>
    /// Applies browser-reported formatting state on the Blazor renderer's synchronization
    /// context.
    /// </summary>
    /// <param name="state">The active inline formatting marks reported by Tiptap.</param>
    /// <returns>A task that completes after the component state refreshes.</returns>
    [JSInvokable]
    public Task OnFormattingStateChanged(TiptapFormattingState state) =>
        InvokeAsync(() =>
        {
            _boldActive = state.Bold;
            _italicActive = state.Italic;
            _strikeActive = state.Strike;
            _codeActive = state.Code;
            StateHasChanged();
        });

    /// <summary>
    /// Executes a browser formatting command when the editor is ready.
    /// </summary>
    /// <param name="command">The command understood by the Tiptap integration module.</param>
    /// <param name="argument">An optional command argument, such as a link target.</param>
    /// <returns>A task that completes after command execution or local error reporting.</returns>
    /// <remarks>Calls made before initialization completes are ignored.</remarks>
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

    /// <summary>
    /// Reads the browser document, converts it to allowlisted inline HTML nodes, and forwards a
    /// successful result to the owner.
    /// </summary>
    /// <returns>A task that completes after conversion, callback delivery, or error reporting.</returns>
    /// <remarks>
    /// Conversion failures and JavaScript errors remain in the open dialog. Calls made before the
    /// editor is ready are ignored.
    /// </remarks>
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

    /// <summary>
    /// Requests that the owning editor close the dialog without saving.
    /// </summary>
    /// <returns>A task that completes when the close callback has finished.</returns>
    protected Task CloseAsync() => Closed.InvokeAsync();

    /// <summary>
    /// Closes the dialog for the Escape key and ignores all other key presses.
    /// </summary>
    /// <param name="args">The keyboard event reported by the dialog.</param>
    /// <returns>A task that completes after any close callback has finished.</returns>
    protected Task HandleKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape"
        ? CloseAsync()
        : Task.CompletedTask;

    /// <summary>
    /// Converts a railway error into compact text suitable for the dialog's error region.
    /// </summary>
    /// <param name="error">The conversion error to format.</param>
    /// <returns>A user-facing description of the error.</returns>
    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        AeroError.NotAllowed notAllowed => notAllowed.msg,
        AeroError.NotFound notFound => notFound.msg,
        AeroError.Conflict conflict => conflict.msg,
        AeroError.Error general => general.msg,
        _ => error.ToString()
    };

    /// <summary>
    /// Adds the active-state class to a formatting button when its mark is selected.
    /// </summary>
    /// <param name="styleClass">The button's base CSS classes.</param>
    /// <param name="active">Whether the associated formatting mark is active.</param>
    /// <returns>The CSS classes for the current state.</returns>
    private static string ToggleButtonClass(string styleClass, bool active) =>
        active ? $"{styleClass} is-active" : styleClass;

    /// <summary>
    /// Converts formatting state to the lowercase token required by <c>aria-pressed</c>.
    /// </summary>
    /// <param name="active">Whether the associated formatting mark is active.</param>
    /// <returns><c>true</c> or <c>false</c>.</returns>
    private static string Pressed(bool active) => active ? "true" : "false";

    /// <summary>
    /// Releases the browser editor and its .NET callback reference.
    /// </summary>
    /// <returns>A value task that completes after owned interop resources are released.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_interop is not null)
            {
                await _interop.DisposeAsync();
            }
        }
        finally
        {
            _callbackReference?.Dispose();
        }
    }

    /// <summary>
    /// Represents the inline formatting marks currently active at the Tiptap selection.
    /// </summary>
    /// <param name="Bold">Whether the bold mark is active.</param>
    /// <param name="Italic">Whether the italic mark is active.</param>
    /// <param name="Strike">Whether the strike-through mark is active.</param>
    /// <param name="Code">Whether the inline-code mark is active.</param>
    public sealed record TiptapFormattingState(
        bool Bold,
        bool Italic,
        bool Strike,
        bool Code);
}
