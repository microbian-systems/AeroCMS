using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Defines an interface for IDocsService.
/// </summary>
public interface IDocsService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// GetPublishedAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(CancellationToken cancellationToken = default);
        /// <summary>
    /// GetPublishedAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(string? culture, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetPagedAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetPublishedBySlugAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetPublishedBySlugAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByIdAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetByIdsAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetByIdsAsync(long[] ids, CancellationToken cancellationToken = default);
        /// <summary>
    /// ListCultureVariantsAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// ForkToCultureAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> ForkToCultureAsync(long id, string targetCulture, string slug, CancellationToken cancellationToken = default);
        /// <summary>
    /// CreateAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> CreateAsync(CreateDocRequest request, CancellationToken cancellationToken = default);
        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> UpdateAsync(long id, UpdateDocRequest request, CancellationToken cancellationToken = default);
        /// <summary>
    /// SaveAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default);
        /// <summary>
    /// SaveFromViewModelAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveFromViewModelAsync(DocViewModel vm, CancellationToken cancellationToken = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetChildrenAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetChildrenAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, string? culture, CancellationToken cancellationToken = default);
        /// <summary>
    /// GetTopLevelCategoriesAsync method.
    /// </summary>
Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default);
}
