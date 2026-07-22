using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Collects Custom HTML source while the owner retains validation authority.</summary>
public partial class CustomHtmlFragmentEditorDialog
{
    private string _source = string.Empty;

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

    /// <summary>Copies the initial source without overwriting in-progress edits on rerender.</summary>
    protected override void OnInitialized() => _source = InitialSource;

    protected Task SaveAsync() => SourceSaved.InvokeAsync(_source);

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task HandleKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;
}
