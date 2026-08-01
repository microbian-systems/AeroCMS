using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Fail-closed hierarchy-query fallback when Content is not installed.</summary>
internal sealed class UnavailableContentHierarchyQueryService : IContentHierarchyQueryService
{
    public Task<Result<ContentQueryResult>> QueryAsync(
        ContentQueryRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Result<ContentQueryResult>>(
            new Result<ContentQueryResult>.Failure(
                AeroError.ConfigurationError(
                    "Content hierarchy queries cannot be rendered because the Content module is unavailable.")));
}
