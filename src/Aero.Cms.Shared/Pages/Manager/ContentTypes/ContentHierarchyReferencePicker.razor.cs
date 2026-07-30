using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Selects one entry from a bounded, server-shaped content hierarchy.
/// </summary>
public partial class ContentHierarchyReferencePicker : IAsyncDisposable
{
    [Parameter] public string? TargetContentTypeAlias { get; set; }
    [Parameter] public string? Culture { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public bool SelectLeafOnly { get; set; } = true;
    [Parameter] public bool ShowAncestors { get; set; } = true;

    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;

    private readonly List<HierarchyReferenceOption> _options = [];
    private CancellationTokenSource? _loadCancellation;
    private string? _loadedAlias;
    private string? _loadedCulture;
    private bool _loadedLeafOnly;
    private bool _loadedShowAncestors;
    private bool _isLoading;
    private string? _error;

    protected override async Task OnParametersSetAsync()
    {
        if (string.Equals(_loadedAlias, TargetContentTypeAlias, StringComparison.Ordinal)
            && string.Equals(_loadedCulture, Culture, StringComparison.Ordinal)
            && _loadedLeafOnly == SelectLeafOnly
            && _loadedShowAncestors == ShowAncestors)
        {
            return;
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();

        _loadedAlias = TargetContentTypeAlias;
        _loadedCulture = Culture;
        _loadedLeafOnly = SelectLeafOnly;
        _loadedShowAncestors = ShowAncestors;
        _options.Clear();
        _error = null;

        if (string.IsNullOrWhiteSpace(TargetContentTypeAlias))
        {
            _error = "Choose a target hierarchy in the content-type settings.";
            return;
        }

        _isLoading = true;
        try
        {
            var result = await ContentItemsApi.GetHierarchyAsync(
                TargetContentTypeAlias,
                Culture,
                _loadCancellation.Token);
            switch (result)
            {
                case Result<ContentHierarchyTreeResult, AeroError>.Ok ok:
                    _options.AddRange(Flatten(ok.Value.Roots));
                    break;
                case Result<ContentHierarchyTreeResult, AeroError>.Failure failure:
                    _error = failure.Error.ToString();
                    break;
            }
        }
        catch (OperationCanceledException) when (_loadCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IEnumerable<HierarchyReferenceOption> Flatten(
        IEnumerable<ContentHierarchyTreeNode> nodes,
        string parentPath = "")
    {
        foreach (var node in nodes)
        {
            var path = string.IsNullOrWhiteSpace(parentPath)
                ? node.Title
                : $"{parentPath} / {node.Title}";
            if (!SelectLeafOnly || node.Children.Count == 0)
            {
                yield return new HierarchyReferenceOption(
                    node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ShowAncestors ? path : node.Title);
            }

            foreach (var child in Flatten(node.Children, path))
            {
                yield return child;
            }
        }
    }

    private Task OnValueChangedAsync(string? value) => ValueChanged.InvokeAsync(value);

    public ValueTask DisposeAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record HierarchyReferenceOption(string Value, string DisplayText);
}
