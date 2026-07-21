using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Presents the existing element property editor as a focused canvas dialog.
/// </summary>
/// <remarks>
/// The dialog does not own or mutate page state. It reuses <see cref="HtmlElementPropertyPanel"/>
/// and forwards every requested mutation to the owning <see cref="PageEditor.PageEditor"/>.
/// </remarks>
public partial class HtmlElementEditorDialog
{
    private ElementReference _closeButton;

    /// <summary>Gets or sets the selected HTML element.</summary>
    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = null!;

    /// <summary>Gets or sets the catalog definition that constrains editable properties.</summary>
    [Parameter, EditorRequired]
    public HtmlElementDefinition Definition { get; set; } = null!;

    /// <summary>Gets or sets the owner-provided property validation error.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the close callback.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Gets or sets the callback for detached property updates.</summary>
    [Parameter]
    public EventCallback<HtmlNodeProperties> PropertiesChanged { get; set; }

    /// <summary>Gets or sets the callback for constrained rich-text editing.</summary>
    [Parameter]
    public EventCallback RichTextRequested { get; set; }

    /// <summary>Gets or sets the callback for guided collection mutations.</summary>
    [Parameter]
    public EventCallback<HtmlCollectionActionKind> CollectionActionRequested { get; set; }

    /// <summary>Gets or sets the callback for subtree duplication.</summary>
    [Parameter]
    public EventCallback DuplicateRequested { get; set; }

    /// <summary>Gets or sets the callback for subtree removal.</summary>
    [Parameter]
    public EventCallback RemoveRequested { get; set; }

    /// <summary>Gets or sets the callback for selecting a media value.</summary>
    [Parameter]
    public EventCallback<HtmlMediaTargetKind> MediaRequested { get; set; }

    /// <summary>Moves keyboard focus into the modal when it is first displayed.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _closeButton.FocusAsync();
        }
    }

    /// <summary>Requests dismissal without changing the current property form.</summary>
    protected Task CloseAsync() => Closed.InvokeAsync();

    /// <summary>Closes the dialog for Escape and ignores other keys.</summary>
    protected Task HandleKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape"
        ? CloseAsync()
        : Task.CompletedTask;
}
