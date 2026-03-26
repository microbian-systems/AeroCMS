using Aero.Core.Railway;

namespace Aero.Cms.Modules.Docs;

public interface IDocsService
{
    Task<Result<string, IReadOnlyList<MarkdownPage>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<string, MarkdownPage?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<string, MarkdownPage?>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<string, MarkdownPage>> SaveAsync(MarkdownPage page, CancellationToken cancellationToken = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<string, IReadOnlyList<MarkdownPage>>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default);
    Task<Result<string, IReadOnlyList<MarkdownPage>>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default);
}
