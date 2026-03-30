using Aero.Core.Railway;
using Marten;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsService(IDocumentSession session) : IDocsService
{
    public async Task<Result<string, IReadOnlyList<DocsPage>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var docs = await session.Query<DocsPage>()
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);
            return Ok<string, IReadOnlyList<DocsPage>>(docs);
        }
        catch (Exception ex)
        {
            return Fail<string, IReadOnlyList<DocsPage>>(ex.Message);
        }
    }

    public async Task<Result<string, DocsPage?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
            return Ok<string, DocsPage?>(doc);
        }
        catch (Exception ex)
        {
            return Fail<string, DocsPage?>(ex.Message);
        }
    }

    public async Task<Result<string, DocsPage?>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await session.LoadAsync<DocsPage>(id, cancellationToken);
            return Ok<string, DocsPage?>(doc);
        }
        catch (Exception ex)
        {
            return Fail<string, DocsPage?>(ex.Message);
        }
    }

    public async Task<Result<string, DocsPage>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);
            return Ok<string, DocsPage>(page);
        }
        catch (Exception ex)
        {
            return Fail<string, DocsPage>(ex.Message);
        }
    }

    public async Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            session.Delete<DocsPage>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Ok<string, bool>(true);
        }
        catch (Exception ex)
        {
            return Fail<string, bool>(ex.Message);
        }
    }

    public async Task<Result<string, IReadOnlyList<DocsPage>>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var children = await session.Query<DocsPage>()
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);
            return Ok<string, IReadOnlyList<DocsPage>>(children);
        }
        catch (Exception ex)
        {
            return Fail<string, IReadOnlyList<DocsPage>>(ex.Message);
        }
    }

    public async Task<Result<string, IReadOnlyList<DocsPage>>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // First find root "docs" page
            var rootDoc = await session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.Slug == "docs", cancellationToken);
            
            if (rootDoc == null)
            {
                return Ok<string, IReadOnlyList<DocsPage>>([]);
            }

            // Find children of root "docs"
            var children = await session.Query<DocsPage>()
                .Where(x => x.ParentId == rootDoc.Id)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            return Ok<string, IReadOnlyList<DocsPage>>(children);
        }
        catch (Exception ex)
        {
            return Fail<string, IReadOnlyList<DocsPage>>(ex.Message);
        }
    }
}
