using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlPageEditorCanvas
{
    [Parameter, EditorRequired]
    public HtmlPageContent Content { get; set; } = new();

    [Parameter]
    public long? SelectedNodeId { get; set; }

    [Parameter]
    public CompiledPageStyles? CompiledStyles { get; set; }

    [Parameter]
    public bool PreviewMode { get; set; }

    [Parameter]
    public EventCallback<long?> SelectionChanged { get; set; }

    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    private Task SelectNodeAsync(long nodeId) => SelectionChanged.InvokeAsync(nodeId);

    private Task ClearSelectionAsync() => PreviewMode
        ? Task.CompletedTask
        : SelectionChanged.InvokeAsync(null);

    private Task RequestFirstSectionAsync() => ElementRequested.InvokeAsync("section");
}
