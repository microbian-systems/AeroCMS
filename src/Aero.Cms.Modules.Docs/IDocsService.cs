using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Modules.Docs;

public interface IDocsService
{
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(string? culture, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetByIdsAsync(long[] ids, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> ForkToCultureAsync(long id, string targetCulture, string slug, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> CreateAsync(CreateDocRequest request, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> UpdateAsync(long id, UpdateDocRequest request, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveFromViewModelAsync(DocViewModel vm, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, string? culture, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default);
}
