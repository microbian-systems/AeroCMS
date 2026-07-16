using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlFragmentImportDialog
{
    private string _fragment = string.Empty;

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback<string> ImportRequested { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    private Task ImportAsync() => ImportRequested.InvokeAsync(_fragment);

    private Task CloseAsync() => Closed.InvokeAsync();
}
