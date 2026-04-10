using Aero.Cms.Core.Entities;
using Aero.Core;
using Marten;
using static global::Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsService(IDocumentSession session) : IDocsService
{
    public async Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var docs = await session.Query<DocsPage>()
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(docs);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = await session.LoadAsync<DocsPage>(id, cancellationToken);
            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);
            return Ok<DocsPage, AeroError>(page);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            session.Delete<DocsPage>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var children = await session.Query<DocsPage>()
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(children);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // First find root "docs" page
            var rootDoc = await session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.Slug == "docs", cancellationToken);
            
            if (rootDoc == null)
            {
                return Ok<IReadOnlyList<DocsPage>, AeroError>([]);
            }

            // Find children of root "docs"
            var children = await session.Query<DocsPage>()
                .Where(x => x.ParentId == rootDoc.Id)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            return Ok<IReadOnlyList<DocsPage>, AeroError>(children);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }
}
