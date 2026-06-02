using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

public interface IContentService
{
    Task<Result<ContentItem, AeroError>> LoadAsync(long id, CancellationToken ct = default);
    Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default);
    Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(long siteId, string contentTypeAlias, string slug, CancellationToken ct = default);
    Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default);
    Task<bool> ExistsAsync(long id, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}
