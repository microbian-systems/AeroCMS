using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageTree;

/// <summary>
/// Real-time URL path preview that calls the tree API to validate
/// the computed path when parent and slug change.
/// </summary>
public partial class PathPreview
{
    [Inject] private IPagesHttpClient PagesClient { get; set; } = null!;

        /// <summary>
    /// Gets or sets the Parent Id.
    /// </summary>
[Parameter]
    public long? ParentId { get; set; }

        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
[Parameter]
    public string Slug { get; set; } = "";

    /// <summary>
    /// When editing an existing page, pass its ID here so the uniqueness
    /// check excludes the page itself (prevents self-conflict).
    /// </summary>
    [Parameter]
    public long? ExcludePageId { get; set; }

    private ComputedPathResult? _result;

        /// <summary>
    /// OnParametersSetAsync method.
    /// </summary>
protected override async Task OnParametersSetAsync()
    {
        await ComputeAsync();
    }

    private async Task ComputeAsync()
    {
        if (string.IsNullOrWhiteSpace(Slug))
        {
            _result = null;
            return;
        }

        try
        {
            var result = await PagesClient.ComputePathAsync(ParentId, Slug, ExcludePageId);
            if (result is Result<ComputedPathResult, AeroError>.Ok ok)
            {
                _result = ok.Value;
            }
            else
            {
                _result = new ComputedPathResult(
                    Slug.StartsWith('/') ? Slug : $"/.../{Slug}",
                    0, false, "validation failed");
            }
        }
        catch
        {
            _result = new ComputedPathResult($"/.../{Slug}", 0, true, null);
        }
    }
}
