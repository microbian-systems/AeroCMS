using Aero.Cms.Core.Entities;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Docs;

public interface IDocsService
{
    Task<Result<string, IReadOnlyList<DocsPage>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<string, DocsPage?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<string, DocsPage?>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<string, DocsPage>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<string, IReadOnlyList<DocsPage>>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default);
    Task<Result<string, IReadOnlyList<DocsPage>>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default);
}
