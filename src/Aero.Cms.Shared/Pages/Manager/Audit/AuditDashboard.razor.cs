using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System.Net.Http.Json;

namespace Aero.Cms.Shared.Pages.Manager.Audit;

/// <summary>
/// Manager audit dashboard — displays a global activity feed from the
/// AeroDB event store (<c>mt_events</c>).  Filters by entity type and
/// date range.  Per-document history is available in the respective
/// editors (version history panel).
/// </summary>
public partial class AuditDashboard : ComponentBase
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private bool _loading;
    private string? _error;
    private readonly List<AuditFeedItem> _items = [];

    private string? _selectedType;
    private DateTime? _fromDate;
    private DateTime? _toDate;

    private static readonly List<string> _typeOptions = ["", "Page", "BlogPost"];

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        await LoadFeedAsync();
    }

    private async Task LoadFeedAsync()
    {
        _loading = true;
        _error = null;
        _items.Clear();

        try
        {
            var url = $"/api/v1/admin/audit?take=100";

            if (!string.IsNullOrWhiteSpace(_selectedType))
                url += $"&type={Uri.EscapeDataString(_selectedType)}";

            if (_fromDate.HasValue)
                url += $"&from={Uri.EscapeDataString(_fromDate.Value.ToString("o"))}";

            if (_toDate.HasValue)
                url += $"&to={Uri.EscapeDataString(_toDate.Value.ToString("o"))}";

            var result = await Http.GetFromJsonAsync<AuditFeedResult>(url);

            if (result?.Items is not null)
                _items.AddRange(result.Items);
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

    private static string GetEventBadgeClass(string eventType) => eventType switch
    {
        "PageCreated" or "BlogPostCreated" => "text-green-600",
        "PageContentUpdated" or "PageCompositionDraftSaved" or "BlogPostContentUpdated" => "text-blue-600",
        "PagePublished" or "PageCompositionPublished" or "BlogPostPublished" or "PageStateChanged" => "text-purple-600",
        "PageDeleted" or "BlogPostDeleted" => "text-red-600",
        "PageMoved" => "text-orange-600",
        _ => "text-gray-600"
    };

    private string FormatEventType(string eventType) => eventType switch
    {
        "PageCreated" => L["Created"],
        "PageContentUpdated" => L["Updated"],
        "PageCompositionDraftSaved" => L["Composition Saved"],
        "PagePublished" => L["Published"],
        "PageCompositionPublished" => L["Composition Published"],
        "PageArchived" => L["Archived"],
        "PageDeleted" => L["Deleted"],
        "PageRestored" => L["Restored"],
        "PageMoved" => L["Moved"],
        "PageVisibilityChanged" => L["Hidden Toggled"],
        "PageStateChanged" => L["State Changed"],
        "BlogPostCreated" => L["Created"],
        "BlogPostContentUpdated" => L["Updated"],
        _ => eventType
    };

    private sealed record AuditFeedResult(int TotalReturned, IReadOnlyList<AuditFeedItem> Items);

    private sealed record AuditFeedItem(
        string StreamKey,
        string EventType,
        long Version,
        DateTime Timestamp,
        bool IsArchived);
}
