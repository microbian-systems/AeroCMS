using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageTree;

/// <summary>
/// Hierarchical dropdown for selecting a parent page. Uses the Pages HTTP client
/// to load the page tree. Excludes a page and its descendants to prevent circular refs.
/// </summary>
public partial class PageTreeSelect
{
    [Inject] private IPagesHttpClient PagesClient { get; set; } = null!;

    /// <summary>
    /// The currently selected parent page ID. <c>null</c> means root level.
    /// </summary>
    [Parameter]
    public long? SelectedParentId { get; set; }

    [Parameter]
    public EventCallback<long?> SelectedParentIdChanged { get; set; }

    /// <summary>
    /// Exclude this page and all descendants from the dropdown to prevent circular references.
    /// </summary>
    [Parameter]
    public long? ExcludePageId { get; set; }

    [Parameter]
    public string Placeholder { get; set; } = "(root — no parent)";

    private List<TreeOption> _options = [];
    private TreeOption? _selectedOption;

    protected override async Task OnInitializedAsync()
    {
        await LoadOptionsAsync();
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            var result = await PagesClient.GetTreeAsync();
            if (result is Result<IReadOnlyList<PageTreeItem>, AeroError>.Ok ok)
            {
                _options = BuildOptions(ok.Value, ExcludePageId);

                if (SelectedParentId.HasValue)
                {
                    _selectedOption = _options.FirstOrDefault(o => o.Id == SelectedParentId.Value);
                }
            }
        }
        catch
        {
            _options = [new() { Id = 0, Label = "(root — no parent)", Depth = 0 }];
        }
    }

    private static List<TreeOption> BuildOptions(
        IReadOnlyList<PageTreeItem> pages, long? excludeId)
    {
        // Compute IDs to exclude
        var excludeIds = new HashSet<long>();
        if (excludeId.HasValue)
        {
            excludeIds.Add(excludeId.Value);
            var excludePath = pages.FirstOrDefault(p => p.Id == excludeId.Value)?.Path;
            if (excludePath is not null)
            {
                foreach (var p in pages.Where(p => p.Path.StartsWith(excludePath + "/")))
                    excludeIds.Add(p.Id);
            }
        }

        var list = new List<TreeOption>
        {
            new() { Id = 0, Label = "(root — no parent)", Depth = 0 }
        };

        foreach (var page in pages.Where(p => !excludeIds.Contains(p.Id)).OrderBy(p => p.Path))
        {
            var indent = page.Depth == 0 ? "" : new string(' ', page.Depth * 3) + "└ ";

            list.Add(new TreeOption
            {
                Id = page.Id,
                Label = $"{indent}{page.Title} ({page.Slug})",
                Depth = page.Depth
            });
        }

        return list;
    }

    private async Task OnSelected(object? value)
    {
        var option = value as TreeOption;
        _selectedOption = option;
        var parentId = option?.Id == 0 ? (long?)null : option?.Id;
        SelectedParentId = parentId;
        await SelectedParentIdChanged.InvokeAsync(parentId);
    }
}

/// <summary>
/// Internal model for dropdown options. Public so Radzen can bind via reflection.
/// </summary>
internal sealed class TreeOption
{
    public long Id { get; set; }
    public string Label { get; set; } = "";
    public int Depth { get; set; }
}
