using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>Owns the visual manager hierarchy, selection, and atomic move intents.</summary>
public partial class ContentHierarchyManager
{
    [Parameter, EditorRequired]
    public string Alias { get; set; } = string.Empty;

    [Parameter]
    public string Title { get; set; } = "Content";

    [Inject]
    private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    private ContentHierarchyTreeResult? _tree;
    private readonly HashSet<long> _expandedIds = [];
    private long? _selectedId;
    private long? _draggedId;
    private string _culture = CultureInfo.CurrentUICulture.Name;
    private string _search = string.Empty;
    private string? _error;
    private string _liveMessage = string.Empty;
    private string? _loadedAlias;
    private bool _isLoading;
    private bool _allExpanded;

    private IReadOnlyList<ContentHierarchyTreeNode> FilteredRoots =>
        FilterNodes(_tree?.Roots ?? [], _search);

    private IReadOnlySet<long> EffectiveExpandedIds
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return _expandedIds;
            }

            var expanded = _expandedIds.ToHashSet();
            foreach (var node in Flatten(FilteredRoots).Where(node => node.Children.Count > 0))
            {
                expanded.Add(node.Id);
            }

            return expanded;
        }
    }

    private ContentHierarchyTreeNode? SelectedNode => _selectedId is { } id
        ? FindNode(id)
        : null;

    private IReadOnlyList<ContentHierarchyTreeNode> SelectedBreadcrumbs
    {
        get
        {
            if (SelectedNode is not { } selected)
            {
                return [];
            }

            var result = new List<ContentHierarchyTreeNode> { selected };
            var current = selected;
            while (FindParent(current.Id) is { } parent)
            {
                result.Add(parent);
                current = parent;
            }

            result.Reverse();
            return result;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (!string.Equals(_loadedAlias, Alias, StringComparison.OrdinalIgnoreCase))
        {
            _loadedAlias = Alias;
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        if (_isLoading || string.IsNullOrWhiteSpace(Alias))
        {
            return;
        }

        _isLoading = true;
        _error = null;
        try
        {
            var result = await ContentItemsApi.GetHierarchyAsync(Alias, _culture);
            if (result is Result<ContentHierarchyTreeResult, AeroError>.Ok ok)
            {
                ApplyTree(ok.Value);
                return;
            }

            if (result is Result<ContentHierarchyTreeResult, AeroError>.Failure failure)
            {
                _error = failure.Error.ToString();
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyTree(ContentHierarchyTreeResult tree)
    {
        _tree = tree;
        _culture = tree.Culture;
        var currentIds = Flatten(tree.Roots).Select(node => node.Id).ToHashSet();
        _expandedIds.RemoveWhere(id => !currentIds.Contains(id));
        if (_selectedId is { } selectedId && !currentIds.Contains(selectedId))
        {
            _selectedId = null;
        }
    }

    private void OnSearchInput(ChangeEventArgs eventArgs)
        => _search = eventArgs.Value?.ToString() ?? string.Empty;

    private async Task OnCultureChangedAsync(ChangeEventArgs eventArgs)
    {
        _culture = eventArgs.Value?.ToString() ?? CultureInfo.CurrentUICulture.Name;
        _selectedId = null;
        _expandedIds.Clear();
        await LoadAsync();
    }

    private void ToggleNode(long id)
    {
        if (!_expandedIds.Add(id))
        {
            _expandedIds.Remove(id);
        }
    }

    private void ToggleExpandAll()
    {
        _allExpanded = !_allExpanded;
        _expandedIds.Clear();
        if (_allExpanded)
        {
            _expandedIds.UnionWith(
                Flatten(_tree?.Roots ?? [])
                    .Where(node => node.Children.Count > 0)
                    .Select(node => node.Id));
        }
    }

    private void SelectNode(ContentHierarchyTreeNode node) => _selectedId = node.Id;

    private void ClearSelection() => _selectedId = null;

    private void StartDrag(long id)
    {
        var node = FindNode(id);
        if (node?.IsTargetType != true)
        {
            return;
        }

        _draggedId = id;
        _liveMessage = $"Picked up “{node.Title}”.";
    }

    private void EndDrag()
    {
        _draggedId = null;
    }

    private async Task HandleDropAsync(ContentHierarchyDropIntent intent)
    {
        if (_draggedId is not { } draggedId || draggedId == intent.TargetId)
        {
            EndDrag();
            return;
        }

        var dragged = FindNode(draggedId);
        var target = FindNode(intent.TargetId);
        if (dragged is null || target is null)
        {
            EndDrag();
            return;
        }

        long? parentId;
        int targetIndex;
        if (intent.Placement == ContentHierarchyDropPlacement.Inside)
        {
            if (!target.CanAcceptChildren)
            {
                _liveMessage = $"“{dragged.Title}” cannot be nested under “{target.Title}”.";
                EndDrag();
                return;
            }

            parentId = target.Id;
            targetIndex = target.Children.Count;
        }
        else
        {
            if (!target.IsTargetType)
            {
                _liveMessage =
                    $"“{dragged.Title}” can only be placed before or after another {Alias} entry.";
                EndDrag();
                return;
            }

            parentId = target.ParentId;
            var siblings = GetSiblings(parentId);
            var targetPosition = siblings.FindIndex(node => node.Id == target.Id);
            targetIndex = targetPosition
                + (intent.Placement == ContentHierarchyDropPlacement.After ? 1 : 0);
            var draggedPosition = siblings.FindIndex(node => node.Id == dragged.Id);
            if (dragged.ParentId == parentId && draggedPosition >= 0 && draggedPosition < targetIndex)
            {
                targetIndex--;
            }
        }

        EndDrag();
        await MoveAsync(dragged, parentId, targetIndex);
    }

    private async Task HandleMoveCommandAsync(ContentHierarchyMoveIntent intent)
    {
        var item = FindNode(intent.ItemId);
        if (item?.IsTargetType != true)
        {
            return;
        }

        var siblings = GetSiblings(item.ParentId);
        var position = siblings.FindIndex(node => node.Id == item.Id);
        switch (intent.Command)
        {
            case ContentHierarchyMoveCommand.Up when position > 0:
                await MoveAsync(item, item.ParentId, position - 1);
                break;
            case ContentHierarchyMoveCommand.Down when position >= 0 && position < siblings.Count - 1:
                await MoveAsync(item, item.ParentId, position + 1);
                break;
            case ContentHierarchyMoveCommand.IntoPrevious when position > 0:
                var previous = siblings[position - 1];
                if (previous.CanAcceptChildren)
                {
                    await MoveAsync(item, previous.Id, previous.Children.Count);
                }
                else
                {
                    _liveMessage = $"“{item.Title}” cannot be nested under “{previous.Title}”.";
                }

                break;
            case ContentHierarchyMoveCommand.Out:
                var parent = FindParent(item.Id);
                if (parent is not null)
                {
                    var parentSiblings = GetSiblings(parent.ParentId);
                    await MoveAsync(
                        item,
                        parent.ParentId,
                        parentSiblings.FindIndex(node => node.Id == parent.Id) + 1);
                }

                break;
            case ContentHierarchyMoveCommand.Root:
                await MoveAsync(item, null, GetSiblings(null).Count);
                break;
        }
    }

    private async Task MoveAsync(ContentHierarchyTreeNode item, long? parentId, int targetIndex)
    {
        var result = await ContentItemsApi.MoveAsync(
            Alias,
            item.Id,
            new MoveContentItemRequest(parentId, Math.Max(0, targetIndex), _culture));
        if (result is Result<ContentHierarchyTreeResult, AeroError>.Ok ok)
        {
            ApplyTree(ok.Value);
            _selectedId = item.Id;
            if (parentId is { } expandedParent)
            {
                _expandedIds.Add(expandedParent);
            }

            _liveMessage = $"Moved “{item.Title}”.";
            return;
        }

        if (result is Result<ContentHierarchyTreeResult, AeroError>.Failure failure)
        {
            _liveMessage = $"“{item.Title}” could not be moved. {failure.Error}";
            Notify(NotificationSeverity.Error, "Move failed", failure.Error.ToString());
        }
    }

    private void CreateRoot()
        => Navigation.NavigateTo($"/manager/content/{Uri.EscapeDataString(Alias)}/editor");

    private void AddChild(ContentHierarchyTreeNode parent)
        => Navigation.NavigateTo(
            $"/manager/content/{Uri.EscapeDataString(Alias)}/editor?parentId={parent.Id}");

    private void Edit(ContentHierarchyTreeNode node)
        => Navigation.NavigateTo(
            $"/manager/content/{Uri.EscapeDataString(node.ContentTypeAlias)}/editor/{node.Id}");

    private async Task DeleteAsync(ContentHierarchyTreeNode node)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete “{node.Title}”? Parent entries must have their children moved first.",
            "Delete entry",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirmed != true)
        {
            return;
        }

        var result = await ContentItemsApi.DeleteAsync(node.ContentTypeAlias, node.Id);
        if (result is Result<bool, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Delete failed", failure.Error.ToString());
            return;
        }

        _selectedId = null;
        await LoadAsync();
    }

    private ContentHierarchyTreeNode? FindNode(long id)
        => Flatten(_tree?.Roots ?? []).FirstOrDefault(node => node.Id == id);

    private ContentHierarchyTreeNode? FindParent(long childId)
        => Flatten(_tree?.Roots ?? [])
            .FirstOrDefault(node => node.Children.Any(child => child.Id == childId));

    private List<ContentHierarchyTreeNode> GetSiblings(long? parentId)
        => (parentId is null
                ? _tree?.Roots ?? []
                : FindNode(parentId.Value)?.Children ?? [])
            .Where(node => node.IsTargetType)
            .ToList();

    private int GetSiblingIndex(ContentHierarchyTreeNode node)
        => Math.Max(0, GetSiblings(node.ParentId).FindIndex(sibling => sibling.Id == node.Id));

    private static IReadOnlyList<ContentHierarchyTreeNode> FilterNodes(
        IReadOnlyList<ContentHierarchyTreeNode> nodes,
        string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return nodes;
        }

        var result = new List<ContentHierarchyTreeNode>();
        foreach (var node in nodes)
        {
            var children = FilterNodes(node.Children, search);
            if (children.Count > 0
                || node.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || node.Slug.Contains(search, StringComparison.OrdinalIgnoreCase)
                || node.ContentTypeAlias.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(node with { Children = children });
            }
        }

        return result;
    }

    private static IEnumerable<ContentHierarchyTreeNode> Flatten(
        IEnumerable<ContentHierarchyTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private void Notify(NotificationSeverity severity, string summary, string detail)
        => NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 5000
        });
}
