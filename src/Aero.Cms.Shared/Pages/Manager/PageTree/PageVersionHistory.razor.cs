using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageTree;

/// <summary>
/// Modal dialog displaying the full event-sourced version history
/// for a page. Shows a timeline of all state-change events from
/// Marten's <c>mt_events</c> table.
/// </summary>
public partial class PageVersionHistory
{
    [Inject] private IPagesHttpClient PagesClient { get; set; } = null!;

    /// <summary>
    /// The page whose version history to display.
    /// </summary>
    [Parameter, EditorRequired]
    public long PageId { get; set; }

    /// <summary>
    /// Display title for the page being viewed.
    /// </summary>
    [Parameter]
    public string PageTitle { get; set; } = "";

    /// <summary>
    /// Raises when the user closes the dialog.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private bool _isOpen;
    private bool _loading;
    private string? _error;
    private PageEventHistory? _history;

    /// <summary>
    /// Opens the dialog and loads the event history.
    /// </summary>
    public async Task OpenAsync()
    {
        _isOpen = true;
        _loading = true;
        _error = null;
        _history = null;

        try
        {
            var result = await PagesClient.GetEventHistoryAsync(PageId);
            if (result is Result<PageEventHistory, AeroError>.Ok ok)
            {
                _history = ok.Value;
            }
            else if (result is Result<PageEventHistory, AeroError>.Failure failure)
            {
                _error = failure.Error.ToString();
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task CloseAsync()
    {
        _isOpen = false;
        await OnClose.InvokeAsync();
    }

    private static string GetEventIcon(string eventType) => eventType switch
    {
        nameof(PageCreated) => "add_circle",
        nameof(PageContentUpdated) => "edit",
        nameof(PagePublished) => "publish",
        nameof(PageArchived) => "archive",
        nameof(PageDeleted) => "delete",
        nameof(PageRestored) => "restore_from_trash",
        nameof(PageMoved) => "drive_file_move",
        nameof(PageVisibilityChanged) => "visibility",
        nameof(PageStateChanged) => "swap_horiz",
        _ => "timeline"
    };

    private static string FormatEventType(string eventType) => eventType switch
    {
        "PageCreated" => "Page Created",
        "PageContentUpdated" => "Content Updated",
        "PagePublished" => "Published",
        "PageArchived" => "Archived",
        "PageDeleted" => "Deleted",
        "PageRestored" => "Restored",
        "PageMoved" => "Moved",
        "PageVisibilityChanged" => "Visibility Changed",
        "PageStateChanged" => "State Changed",
        _ => eventType
    };

    private static string GetDotClass(bool isLatest) =>
        isLatest
            ? "w-[35px] h-[35px] rounded-full flex items-center justify-center bg-blue-100"
            : "w-[35px] h-[35px] rounded-full flex items-center justify-center bg-gray-100";
}
