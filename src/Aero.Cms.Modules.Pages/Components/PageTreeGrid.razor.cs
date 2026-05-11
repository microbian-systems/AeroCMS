using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Marten;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Modules.Pages.Components;

/// <summary>
/// Page tree manager using Radzen DataGrid self-referencing hierarchy.
/// Loads root pages on init and lazy-loads children on expand via <c>LoadChildData</c>.
/// Uses direct DI for Blazor Server (no HTTP round-trip).
/// </summary>
public partial class PageTreeGrid
{
    [Inject] private IPageTreeService TreeService { get; set; } = null!;
    [Inject] private INavigationService NavService { get; set; } = null!;
    [Inject] private IQuerySession QuerySession { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private DialogService DialogService { get; set; } = null!;

    private RadzenDataGrid<TreeNodeModel>? _grid;
    private List<TreeNodeModel> _rootNodes = [];
    private TreeNodeModel? _selectedNode;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await LoadRootNodesAsync();
    }

    // ────────────────────────────────────────────────────────────────
    //  Root Loading
    // ────────────────────────────────────────────────────────────────

    private async Task LoadRootNodesAsync()
    {
        _error = null;
        try
        {
            var result = await TreeService.GetChildrenAsync(parentId: null);
            if (result is not Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
            {
                _error = "Failed to load page tree.";
                return;
            }

            var pages = ok.Value;
            var ids = pages.Select(p => p.Id).ToList();
            var expandableSet = await GetExpandableIdsAsync(ids);

            _rootNodes = pages.Select(p => new TreeNodeModel
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Path = p.Path,
                Depth = p.Depth,
                Order = p.Order,
                ParentId = p.ParentId,
                PublicationState = p.PublicationState,
                IsHidden = p.IsHidden,
                HasChildren = expandableSet.Contains(p.Id)
            }).ToList();
        }
        catch (Exception ex)
        {
            _error = $"Error loading tree: {ex.Message}";
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Lazy-load children (fired by Radzen on expand)
    // ────────────────────────────────────────────────────────────────

    private async Task LoadChildData(DataGridLoadChildDataEventArgs<TreeNodeModel> args)
    {
        try
        {
            var result = await TreeService.GetChildrenAsync(args.Item!.Id);
            if (result is not Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
                return;

            var pages = ok.Value;
            var ids = pages.Select(p => p.Id).ToList();
            var expandableSet = await GetExpandableIdsAsync(ids);

            args.Data = pages.Select(p => new TreeNodeModel
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Path = p.Path,
                Depth = p.Depth,
                Order = p.Order,
                ParentId = p.ParentId,
                PublicationState = p.PublicationState,
                IsHidden = p.IsHidden,
                HasChildren = expandableSet.Contains(p.Id)
            }).ToList();
        }
        catch (Exception ex)
        {
            _error = $"Error loading children: {ex.Message}";
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Row expandability
    // ────────────────────────────────────────────────────────────────

    private void RowRender(RowRenderEventArgs<TreeNodeModel> args)
    {
        args.Expandable = args.Data.HasChildren;
    }

    // ────────────────────────────────────────────────────────────────
    //  Expandability helper: single-batch check for HasChildren
    // ────────────────────────────────────────────────────────────────

    private async Task<HashSet<long>> GetExpandableIdsAsync(IReadOnlyList<long> pageIds)
    {
        if (pageIds.Count == 0) return [];

        var parentIds = await QuerySession.Query<PageDocument>()
            .Where(x => x.ParentId != null && x.ParentId.Value.IsOneOf(pageIds.ToArray()))
            .Select(x => x.ParentId!.Value)
            .ToListAsync();

        return [..parentIds.Distinct()];
    }

    // ────────────────────────────────────────────────────────────────
    //  Refresh
    // ────────────────────────────────────────────────────────────────

    private async Task ReloadAsync()
    {
        await LoadRootNodesAsync();
        await (_grid?.Reload() ?? Task.CompletedTask);
    }

    // ────────────────────────────────────────────────────────────────
    //  Badge / Actions
    // ────────────────────────────────────────────────────────────────

    private static BadgeStyle GetBadgeStyle(ContentPublicationState state) => state switch
    {
        ContentPublicationState.Published => BadgeStyle.Success,
        ContentPublicationState.Draft => BadgeStyle.Secondary,
        ContentPublicationState.Archived => BadgeStyle.Dark,
        ContentPublicationState.InReview => BadgeStyle.Warning,
        ContentPublicationState.Scheduled => BadgeStyle.Info,
        _ => BadgeStyle.Light
    };

    private async Task EditNodeAsync(TreeNodeModel node)
    {
        NavigationManager.NavigateTo($"/_cms/manager/pages/{node.Id}");
    }

    private async Task ToggleHiddenAsync(TreeNodeModel node)
    {
        var result = await NavService.SetHiddenAsync(node.Id, !node.IsHidden);
        if (result.IsSuccess) await ReloadAsync();
    }

    private async Task ConfirmDeleteAsync(TreeNodeModel node)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete page '{node.Title}' and all its descendants? This is a soft delete.",
            "Confirm Delete",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirmed == true) await ReloadAsync();
    }

    // ────────────────────────────────────────────────────────────────
    //  TreeNodeModel (self-referencing for Radzen hierarchy)
    // ────────────────────────────────────────────────────────────────

    public sealed class TreeNodeModel
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Path { get; set; } = "/";
        public int Depth { get; set; }
        public int Order { get; set; }
        public long? ParentId { get; set; }
        public ContentPublicationState PublicationState { get; set; }
        public bool IsHidden { get; set; }
        public bool HasChildren { get; set; }
        /// <summary>
        /// Children populated by Radzen on expand via <c>LoadChildData</c>.
        /// Radzen internally manages this collection.
        /// </summary>
        public List<TreeNodeModel> Children { get; set; } = [];
    }
}
