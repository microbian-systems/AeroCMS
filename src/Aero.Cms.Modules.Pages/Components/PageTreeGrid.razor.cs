using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Modules.Pages.Components;

/// <summary>
/// Page tree manager using a Radzen DataGrid with depth-based indentation.
/// Injects IPageTreeService and INavigationService directly (no HTTP round-trip).
/// </summary>
public partial class PageTreeGrid
{
    [Inject] private IPageTreeService TreeService { get; set; } = null!;
    [Inject] private INavigationService NavService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private DialogService DialogService { get; set; } = null!;

    private List<TreeNodeModel> _allNodes = [];
    private TreeNodeModel? _selectedNode;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await LoadTreeAsync();
    }

    private async Task LoadTreeAsync()
    {
        _error = null;
        try
        {
            var result = await TreeService.GetTreeAsync();
            if (result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
            {
                _allNodes = ok.Value.Select(p => new TreeNodeModel
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
                    HasChildren = ok.Value.Any(x => x.ParentId == p.Id)
                }).ToList();
            }
            else
            {
                _error = "Failed to load page tree.";
            }
        }
        catch (Exception ex)
        {
            _error = $"Error loading tree: {ex.Message}";
        }
    }

    private string GetIndentClass(int depth) => depth switch
    {
        0 => "", 1 => "ml-6", 2 => "ml-12", 3 => "ml-18",
        4 => "ml-24", _ => "ml-30"
    };

    private BadgeStyle GetBadgeStyle(ContentPublicationState state) => state switch
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
        if (result.IsSuccess) await LoadTreeAsync();
    }

    private async Task ConfirmDeleteAsync(TreeNodeModel node)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete page '{node.Title}' and all its descendants? This is a soft delete.",
            "Confirm Delete",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirmed == true) await LoadTreeAsync();
    }

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
    }
}
