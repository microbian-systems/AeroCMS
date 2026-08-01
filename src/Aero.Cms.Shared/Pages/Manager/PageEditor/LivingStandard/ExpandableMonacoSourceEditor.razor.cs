using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Hosts a reusable Monaco source editor with an expandable presentation.</summary>
public partial class ExpandableMonacoSourceEditor
{
    private StandaloneCodeEditor? _editor;
    private string _source = string.Empty;

    /// <summary>Gets or sets the stable DOM identifier for the Monaco editor.</summary>
    [Parameter, EditorRequired]
    public string EditorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Monaco language identifier.</summary>
    [Parameter, EditorRequired]
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets the source-kind label announced for editor controls.</summary>
    [Parameter, EditorRequired]
    public string AccessibleLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the compact renderer name shown in the source toolbar.</summary>
    [Parameter, EditorRequired]
    public string RendererLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the initial source copied before Monaco construction.</summary>
    [Parameter, EditorRequired]
    public string InitialSource { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the owning surface is expanded.</summary>
    [Parameter]
    public bool IsExpanded { get; set; }

    /// <summary>Gets or sets the callback used to request a surface expansion change.</summary>
    [Parameter]
    public EventCallback<bool> IsExpandedChanged { get; set; }

    /// <summary>Gets or sets the callback notified when Monaco source changes.</summary>
    [Parameter]
    public EventCallback<string> SourceChanged { get; set; }

    /// <summary>Gets or sets whether the configured AI feature is usable.</summary>
    [Parameter]
    public bool AiEnabled { get; set; }

    /// <summary>Gets or sets the discoverable reason that AI assistance is unavailable.</summary>
    [Parameter]
    public string AiUnavailableMessage { get; set; }
        = "Configure and enable an AI provider to use source assistance.";

    /// <summary>Gets or sets the callback that opens the manager AI workflow.</summary>
    [Parameter]
    public EventCallback AiRequested { get; set; }

    /// <summary>Gets or sets the callback that previews the current source page.</summary>
    [Parameter]
    public EventCallback PreviewRequested { get; set; }

    private string ExpansionLabel => IsExpanded
        ? $"Restore {AccessibleLabel}"
        : $"Expand {AccessibleLabel}";

    private string LanguageLabel => Language switch
    {
        "liquid" => "Template",
        "typescript" => "TypeScript",
        "html" => "HTML",
        _ => Language
    };

    private string AiButtonTitle => AiEnabled
        ? "Open AI assistant"
        : AiUnavailableMessage;

    /// <summary>Copies source once so parent rerenders do not overwrite in-progress edits.</summary>
    protected override void OnInitialized() => _source = InitialSource;

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor) => new()
    {
        AutomaticLayout = true,
        AriaLabel = AccessibleLabel,
        Language = Language,
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
            var source = await _editor.GetValue();
            if (!string.Equals(_source, source, StringComparison.Ordinal))
            {
                _source = source;
                await SourceChanged.InvokeAsync(source);
            }
        }
    }

    /// <summary>Reads the current source from Monaco without synchronizing on each keystroke.</summary>
    public async Task<string> GetValueAsync()
    {
        if (_editor is not null)
        {
            _source = await _editor.GetValue();
        }

        return _source;
    }

    /// <summary>Replaces Monaco source explicitly when the owning page loads a new snapshot.</summary>
    public async Task SetValueAsync(string source)
    {
        source ??= string.Empty;
        _source = source;

        if (_editor is not null
            && !string.Equals(await _editor.GetValue(), source, StringComparison.Ordinal))
        {
            await _editor.SetValue(source);
        }
    }

    private Task ToggleExpandedAsync() => IsExpandedChanged.InvokeAsync(!IsExpanded);

    private Task RequestAiAsync() => AiEnabled
        ? AiRequested.InvokeAsync()
        : Task.CompletedTask;
}
