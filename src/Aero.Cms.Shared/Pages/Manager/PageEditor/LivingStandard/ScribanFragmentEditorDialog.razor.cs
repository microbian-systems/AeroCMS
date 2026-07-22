using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Authors a PageEditor Scriban fragment with the existing Monaco component.</summary>
public partial class ScribanFragmentEditorDialog
{
    private StandaloneCodeEditor? _editor;
    private string _source = string.Empty;
    private bool _isSaving;

    /// <summary>Gets or sets the stable fragment node identifier used for the editor DOM ID.</summary>
    [Parameter, EditorRequired]
    public long NodeId { get; set; }

    /// <summary>Gets or sets the initial Scriban source.</summary>
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

    protected string EditorId => $"page-scriban-fragment-{NodeId}";

    /// <summary>Copies initial source before Monaco construction.</summary>
    protected override void OnInitialized() => _source = InitialSource;

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor) => new()
    {
        AutomaticLayout = true,
        Language = "liquid",
        Value = _source,
        Minimap = new EditorMinimapOptions { Enabled = false },
        ScrollBeyondLastLine = false,
        WordWrap = "on",
        LineNumbers = "on",
        TabSize = 2
    };

    private async Task OnEditorContentChangedAsync()
    {
        if (_editor is not null)
        {
            _source = await _editor.GetValue();
        }
    }

    protected async Task SaveAsync()
    {
        _isSaving = true;
        try
        {
            if (_editor is not null)
            {
                _source = await _editor.GetValue();
            }

            await SourceSaved.InvokeAsync(_source);
        }
        finally
        {
            _isSaving = false;
        }
    }

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;
}
