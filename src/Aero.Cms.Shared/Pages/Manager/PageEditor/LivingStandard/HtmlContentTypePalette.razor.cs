using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Displays content-type metadata supplied by the page editor orchestrator.
/// </summary>
public partial class HtmlContentTypePalette
{
    /// <summary>Gets or sets the available content types.</summary>
    [Parameter]
    public IReadOnlyList<ContentTypeSummary> ContentTypes { get; set; } = [];

    /// <summary>Gets or sets the selected content-type alias.</summary>
    [Parameter]
    public string? SelectedAlias { get; set; }

    /// <summary>Gets or sets the selected content-type definition.</summary>
    [Parameter]
    public ContentTypeDetail? SelectedContentType { get; set; }

    /// <summary>Gets or sets whether content metadata is loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Gets or sets a content loading error.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Raised when the selected content type changes.</summary>
    [Parameter]
    public EventCallback<string?> SelectedAliasChanged { get; set; }

    /// <summary>Raised when the author requests fresh content-type metadata.</summary>
    [Parameter]
    public EventCallback RefreshRequested { get; set; }

    private Task OnTypeChangedAsync(ChangeEventArgs args) =>
        SelectedAliasChanged.InvokeAsync(args.Value?.ToString());

    private Task RefreshAsync() => RefreshRequested.InvokeAsync();
}
