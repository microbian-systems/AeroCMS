using System.Text.RegularExpressions;
using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Aero.Cms.Shared.Components.MarkdownEditor;

/// <summary>
/// Reusable visual Markdown authoring control backed by Tiptap and Aero's
/// validated, lossless HTML-to-Markdown interchange boundary.
/// </summary>
public partial class AeroMarkdownEditor
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly IHtmlContentModelPolicy ContentPolicy = new HtmlContentModelPolicy(Catalog);

    private ElementReference _editorElement;
    private TiptapMarkdownEditorInterop? _interop;
    private DotNetObjectReference<AeroMarkdownEditor>? _callbackReference;
    private TiptapMarkdownFormattingState _formatting = new();
    private bool _initializationAttempted;
    private bool _hasUnsynchronizedChanges;
    private bool _isReady;
    private string? _error;
    private string _markdown = string.Empty;
    private string? _pendingMarkdown;
    private bool _showLinkPanel;
    private string _linkText = string.Empty;
    private string _linkUrl = string.Empty;
    private string _linkTitle = string.Empty;
    private bool _linkWasActive;
    private bool _showImagePanel;
    private string? _imageUrl;
    private string? _imageAlt;
    private string? _imageTitle;
    private bool _imageDecorative;
    private bool _showTablePanel;
    private int _tableRows = 3;
    private int _tableColumns = 3;

    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = null!;

    /// <summary>Gets or sets the authoritative Markdown value.</summary>
    [Parameter] public string Markdown { get; set; } = string.Empty;

    /// <summary>Receives Markdown after the visual document has synchronized successfully.</summary>
    [Parameter] public EventCallback<string> MarkdownChanged { get; set; }

    /// <summary>Notifies the owner immediately when the browser editor changes.</summary>
    [Parameter] public EventCallback ContentChanged { get; set; }

    /// <summary>Requests the owning feature's media selector.</summary>
    [Parameter] public EventCallback ChooseMediaRequested { get; set; }

    /// <summary>Requests the owning feature's AI enhancement workflow.</summary>
    [Parameter] public EventCallback EnhanceRequested { get; set; }

    /// <summary>Gets or sets whether a configured AI provider can enhance content.</summary>
    [Parameter] public bool AiEnabled { get; set; }

    /// <summary>Gets or sets the tooltip shown when AI enhancement is unavailable.</summary>
    [Parameter] public string AiUnavailableMessage { get; set; } =
        "Configure and enable an AI provider before enhancing content.";

    /// <summary>Gets or sets the accessible editor label.</summary>
    [Parameter] public string AriaLabel { get; set; } = "Markdown body";

    /// <summary>Gets or sets a stable DOM identifier prefix for panel labels.</summary>
    [Parameter] public string EditorId { get; set; } = "aero-markdown-editor";

    private bool CanInsertImage =>
        !string.IsNullOrWhiteSpace(_imageUrl)
        && (_imageDecorative || !string.IsNullOrWhiteSpace(_imageAlt));

    private bool CanApplyLink =>
        !string.IsNullOrWhiteSpace(_linkText)
        && !string.IsNullOrWhiteSpace(_linkUrl);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var parameterValue = Markdown ?? string.Empty;
        if (!_initializationAttempted)
        {
            _markdown = parameterValue;
            return;
        }

        if (!string.Equals(parameterValue, _markdown, StringComparison.Ordinal))
        {
            _pendingMarkdown = parameterValue;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isReady && !_initializationAttempted)
        {
            _initializationAttempted = true;
            await InitializeAsync();
            return;
        }

        if (_isReady && _pendingMarkdown is not null)
        {
            var pendingMarkdown = _pendingMarkdown;
            _pendingMarkdown = null;
            await SetMarkdownAsync(pendingMarkdown);
        }
    }

    private async Task InitializeAsync()
    {
        var rendered = ConvertMarkdownToHtml(_markdown);
        if (rendered is Result<string>.Failure failure)
        {
            _error = $"The Markdown content cannot be opened safely: {FormatError(failure.Error)}";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            _interop = new TiptapMarkdownEditorInterop(JS);
            _callbackReference = DotNetObjectReference.Create(this);
            await _interop.InitializeAsync(
                _editorElement,
                ((Result<string>.Ok)rendered).Value,
                _callbackReference);
            _hasUnsynchronizedChanges = false;
            _isReady = true;
            _error = null;
            await InvokeAsync(StateHasChanged);
        }
        catch (JSException exception)
        {
            _error = $"The browser Markdown editor could not be loaded: {exception.Message}";
            await DisposeInteropAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Converts the current visual document to validated Markdown and updates the bound value.
    /// </summary>
    public async Task<Result<string>> SynchronizeAsync()
    {
        if (_interop is null || !_isReady)
        {
            return new Result<string>.Ok(_markdown);
        }

        if (!_hasUnsynchronizedChanges)
        {
            return new Result<string>.Ok(_markdown);
        }

        try
        {
            var exported = ConvertHtmlToMarkdown(await _interop.GetHtmlAsync());
            if (exported is Result<string>.Failure failure)
            {
                _error = $"The edited content cannot be saved as Markdown: {FormatError(failure.Error)}";
                await InvokeAsync(StateHasChanged);
                return failure;
            }

            var synchronized = ((Result<string>.Ok)exported).Value;
            _markdown = synchronized;
            _hasUnsynchronizedChanges = false;
            _error = null;
            await MarkdownChanged.InvokeAsync(synchronized);
            return new Result<string>.Ok(synchronized);
        }
        catch (JSException exception)
        {
            _error = $"The edited Markdown content could not be read: {exception.Message}";
            await InvokeAsync(StateHasChanged);
            return new Result<string>.Failure(new AeroError.Error(_error));
        }
    }

    /// <summary>Replaces the visual document from an authoritative Markdown value.</summary>
    public async Task<Result<string>> SetMarkdownAsync(string markdown)
    {
        markdown ??= string.Empty;
        var rendered = ConvertMarkdownToHtml(markdown);
        if (rendered is Result<string>.Failure failure)
        {
            _error = $"The Markdown content cannot be shown visually: {FormatError(failure.Error)}";
            await InvokeAsync(StateHasChanged);
            return failure;
        }

        if (_interop is null || !_isReady)
        {
            _markdown = markdown;
            return new Result<string>.Ok(markdown);
        }

        try
        {
            await _interop.SetHtmlAsync(((Result<string>.Ok)rendered).Value);
            _markdown = markdown;
            _hasUnsynchronizedChanges = false;
            _error = null;
            return new Result<string>.Ok(markdown);
        }
        catch (JSException exception)
        {
            _error = $"The visual Markdown editor could not be updated: {exception.Message}";
            await InvokeAsync(StateHasChanged);
            return new Result<string>.Failure(new AeroError.Error(_error));
        }
    }

    /// <summary>Populates the pending image form from an owning media selector.</summary>
    public Task SetImageSelectionAsync(string source, string? alternativeText)
    {
        _imageUrl = source;
        if (string.IsNullOrWhiteSpace(_imageAlt))
        {
            _imageAlt = alternativeText;
        }

        _showImagePanel = true;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ExecuteAsync(string command)
    {
        if (_interop is null || !_isReady)
        {
            return;
        }

        try
        {
            _error = null;
            if (await _interop.ExecuteAsync(command))
            {
                await MarkChangedAsync();
            }
        }
        catch (JSException exception)
        {
            _error = $"The Markdown formatting command failed: {exception.Message}";
        }
    }

    private async Task ToggleLinkPanelAsync()
    {
        _showLinkPanel = !_showLinkPanel;
        if (!_showLinkPanel || _interop is null || !_isReady)
        {
            return;
        }

        CloseOtherPanels();
        _showLinkPanel = true;
        try
        {
            var context = await _interop.GetLinkContextAsync();
            _linkText = context.Text ?? string.Empty;
            _linkUrl = context.Href ?? string.Empty;
            _linkTitle = context.Title ?? string.Empty;
            _linkWasActive = context.Active;
        }
        catch (JSException exception)
        {
            _error = $"The selected link could not be read: {exception.Message}";
        }
    }

    private async Task ApplyLinkAsync()
    {
        if (_interop is null || !CanApplyLink)
        {
            return;
        }

        try
        {
            var applied = await _interop.SetLinkAsync(
                _linkText.Trim(),
                _linkUrl.Trim(),
                string.IsNullOrWhiteSpace(_linkTitle) ? null : _linkTitle.Trim());
            if (!applied)
            {
                _error = "The link could not be applied at the current selection.";
                return;
            }

            _showLinkPanel = false;
            _error = null;
            await MarkChangedAsync();
        }
        catch (JSException exception)
        {
            _error = $"The link could not be applied: {exception.Message}";
        }
    }

    private async Task RemoveLinkAsync()
    {
        if (_interop is null)
        {
            return;
        }

        try
        {
            if (await _interop.RemoveLinkAsync())
            {
                _showLinkPanel = false;
                _error = null;
                await MarkChangedAsync();
            }
        }
        catch (JSException exception)
        {
            _error = $"The link could not be removed: {exception.Message}";
        }
    }

    private void ToggleImagePanel()
    {
        _showImagePanel = !_showImagePanel;
        if (_showImagePanel)
        {
            CloseOtherPanels();
            _showImagePanel = true;
        }
    }

    private void ToggleTablePanel()
    {
        _showTablePanel = !_showTablePanel;
        if (_showTablePanel)
        {
            CloseOtherPanels();
            _showTablePanel = true;
        }
    }

    private void CloseOtherPanels()
    {
        _showLinkPanel = false;
        _showImagePanel = false;
        _showTablePanel = false;
    }

    private async Task InsertImageAsync()
    {
        if (_interop is null || !CanInsertImage)
        {
            _error = "Choose an image and provide alternative text, or mark it decorative.";
            return;
        }

        try
        {
            var inserted = await _interop.InsertImageAsync(
                _imageUrl!,
                _imageDecorative ? string.Empty : _imageAlt!.Trim(),
                _imageTitle);
            if (!inserted)
            {
                _error = "The image could not be inserted at the current selection.";
                return;
            }

            _error = null;
            _showImagePanel = false;
            _imageUrl = null;
            _imageAlt = null;
            _imageTitle = null;
            _imageDecorative = false;
            await MarkChangedAsync();
        }
        catch (Exception exception)
        {
            _error = $"The image could not be inserted: {exception.Message}";
        }
    }

    private async Task InsertTableAsync()
    {
        if (_interop is null)
        {
            return;
        }

        if (_tableRows is < 2 or > 10 || _tableColumns is < 1 or > 10)
        {
            _error = "Table dimensions must be between 2 and 10 rows and 1 and 10 columns.";
            return;
        }

        try
        {
            if (!await _interop.InsertTableAsync(_tableRows, _tableColumns))
            {
                _error = "The table could not be inserted at the current selection.";
                return;
            }

            _error = null;
            _showTablePanel = false;
            await MarkChangedAsync();
        }
        catch (Exception exception)
        {
            _error = $"The table could not be inserted: {exception.Message}";
        }
    }

    private async Task RequestEnhancementAsync()
    {
        if (AiEnabled)
        {
            await EnhanceRequested.InvokeAsync();
        }
    }

    private async Task RequestMediaAsync() => await ChooseMediaRequested.InvokeAsync();

    private async Task MarkChangedAsync()
    {
        _hasUnsynchronizedChanges = true;
        await ContentChanged.InvokeAsync();
    }

    /// <summary>Marks the document dirty when Tiptap reports a browser edit.</summary>
    [JSInvokable]
    public Task OnTiptapContentChanged() => MarkChangedAsync();

    /// <summary>Updates the active-format state reported by Tiptap.</summary>
    [JSInvokable]
    public Task OnTiptapFormattingStateChanged(TiptapMarkdownFormattingState state)
    {
        _formatting = state;
        return InvokeAsync(StateHasChanged);
    }

    private async Task RetryAsync()
    {
        await DisposeInteropAsync();
        _initializationAttempted = false;
        _error = null;
        await InvokeAsync(StateHasChanged);
    }

    private static string ToolClass(bool active) =>
        active ? "aero-markdown-tool is-active" : "aero-markdown-tool";

    private static IHtmlFragmentImporter CreateHtmlImporter()
    {
        var attributePolicy = new HtmlAttributePolicy();
        return new HtmlFragmentImporter(
            Catalog,
            attributePolicy,
            ContentPolicy,
            new HtmlContentValidator(Catalog, ContentPolicy, attributePolicy));
    }

    private static IMarkdownInterchangeAdapter CreateMarkdownAdapter()
    {
        var attributePolicy = new HtmlAttributePolicy();
        return new MarkdownInterchangeAdapter(
            CreateHtmlImporter(),
            new HtmlContentValidator(Catalog, ContentPolicy, attributePolicy));
    }

    private static HtmlStaticRenderer CreateRenderer()
    {
        var attributePolicy = new HtmlAttributePolicy();
        return new HtmlStaticRenderer(
            Catalog,
            ContentPolicy,
            attributePolicy,
            new HtmlContentValidator(Catalog, ContentPolicy, attributePolicy));
    }

    private static Result<string> ConvertMarkdownToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new Result<string>.Ok(string.Empty);
        }

        var imported = CreateMarkdownAdapter().Import(markdown);
        return imported is Result<HtmlPageContent>.Ok ok
            ? CreateRenderer().Render(ok.Value)
            : ((Result<HtmlPageContent>.Failure)imported).Error;
    }

    private static Result<string> ConvertHtmlToMarkdown(string html)
    {
        if (IsEmptyEditorHtml(html))
        {
            return new Result<string>.Ok(string.Empty);
        }

        var imported = CreateHtmlImporter().Import(html);
        return imported is Result<HtmlPageContent>.Ok ok
            ? CreateMarkdownAdapter().Export(ok.Value)
            : ((Result<HtmlPageContent>.Failure)imported).Error;
    }

    private static bool IsEmptyEditorHtml(string html) =>
        string.IsNullOrWhiteSpace(html)
        || Regex.IsMatch(
            html,
            @"^(?:\s*<p>\s*(?:<br\s*/?>)?\s*</p>\s*)+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        _ => error.ToString()
    };

    private async Task DisposeInteropAsync()
    {
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
            _interop = null;
        }

        _callbackReference?.Dispose();
        _callbackReference = null;
        _isReady = false;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await DisposeInteropAsync();

    /// <summary>Formatting marks and block context active at the current selection.</summary>
    public sealed record TiptapMarkdownFormattingState(
        bool Paragraph = false,
        bool Heading2 = false,
        bool Heading3 = false,
        bool BulletList = false,
        bool OrderedList = false,
        bool Blockquote = false,
        bool CodeBlock = false,
        bool Bold = false,
        bool Italic = false,
        bool Strike = false,
        bool Code = false,
        bool Link = false,
        bool Table = false);
}
