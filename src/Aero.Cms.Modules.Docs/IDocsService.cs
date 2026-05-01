using Aero.Cms.Core.Entities;
using Aero.Core;
namespace Aero.Cms.Modules.Docs;

public interface IDocsService
{
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default);
    Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default);
}
