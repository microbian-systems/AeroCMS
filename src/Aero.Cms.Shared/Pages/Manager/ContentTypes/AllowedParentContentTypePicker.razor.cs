using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>Presents a searchable, explicit allowlist of cross-type hierarchy parents.</summary>
public partial class AllowedParentContentTypePicker
{
    private string _search = string.Empty;

    /// <summary>Gets or sets the current-site content types available for selection.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ContentTypeSummary> ContentTypes { get; set; } = [];

    /// <summary>Gets or sets the selected content-type identifiers.</summary>
    [Parameter]
    public IReadOnlyList<long> SelectedIds { get; set; } = [];

    /// <summary>Gets or sets the callback invoked with an immutable selected-ID snapshot.</summary>
    [Parameter]
    public EventCallback<IReadOnlyList<long>> SelectedIdsChanged { get; set; }

    private IReadOnlyList<ContentTypeSummary> VisibleTypes => ContentTypes
        .Where(contentType => contentType.Id > 0)
        .Where(contentType => string.IsNullOrWhiteSpace(_search)
            || contentType.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
            || contentType.Alias.Contains(_search, StringComparison.OrdinalIgnoreCase))
        .OrderBy(contentType => contentType.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private bool IsSelected(long id) => SelectedIds.Contains(id);

    private Task SetSelectedAsync(long id, bool selected)
    {
        var next = SelectedIds.ToHashSet();
        if (selected)
        {
            next.Add(id);
        }
        else
        {
            next.Remove(id);
        }

        return SelectedIdsChanged.InvokeAsync(next.Order().ToArray());
    }
}
