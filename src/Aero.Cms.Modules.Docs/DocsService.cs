using Aero.Core.Railway;
using Marten;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsService(IDocumentSession session) : IDocsService
{
    public async Task<Result<string, IReadOnlyList<MarkdownPage>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var docs = await session.Query<MarkdownPage>()
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);
            return Ok<string, IReadOnlyList<MarkdownPage>>(docs);
        }
        catch (Exception ex)
        {
            return Fail<string, IReadOnlyList<MarkdownPage>>(ex.Message);
        }
    }

    public async Task<Result<string, MarkdownPage?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await session.Query<MarkdownPage>()
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
            return Ok<string, MarkdownPage?>(doc);
        }
        catch (Exception ex)
        {
            return Fail<string, MarkdownPage?>(ex.Message);
        }
    }

    public async Task<Result<string, MarkdownPage?>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await session.LoadAsync<MarkdownPage>(id, cancellationToken);
            return Ok<string, MarkdownPage?>(doc);
        }
        catch (Exception ex)
        {
            return Fail<string, MarkdownPage?>(ex.Message);
        }
    }

    public async Task<Result<string, MarkdownPage>> SaveAsync(MarkdownPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);
            return Ok<string, MarkdownPage>(page);
        }
        catch (Exception ex)
        {
            return Fail<string, MarkdownPage>(ex.Message);
        }
    }

    public async Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            session.Delete<MarkdownPage>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Ok<string, bool>(true);
        }
        catch (Exception ex)
        {
            return Fail<string, bool>(ex.Message);
        }
    }

    public async Task<Result<string, IReadOnlyList<MarkdownPage>>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var children = await session.Query<MarkdownPage>()
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);
            return Ok<string, IReadOnlyList<MarkdownPage>>(children);
        }
        catch (Exception ex)
        {
            return Fail<string, IReadOnlyList<MarkdownPage>>(ex.Message);
        }
    }

    public async Task<Result<string, IReadOnlyList<MarkdownPage>>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // First find root "docs" page
            var rootDoc = await session.Query<MarkdownPage>()
                .FirstOrDefaultAsync(x => x.Slug == "docs", cancellationToken);
            
            if (rootDoc == null)
            {
                return Ok<string, IReadOnlyList<MarkdownPage>>([]);
            }

            // Find children of root "docs"
            var children = await session.Query<MarkdownPage>()
                .Where(x => x.ParentId == rootDoc.Id)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            return Ok<string, IReadOnlyList<MarkdownPage>>(children);
        }
        catch (Exception ex)
        {
            return Fail<string, IReadOnlyList<MarkdownPage>>(ex.Message);
        }
    }
}
