using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Marten;
using Wolverine;
using static global::Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsService(IDocumentSession session, IMessageBus bus) : IDocsService
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
            var existing = await session.LoadAsync<DocsPage>(page.Id, cancellationToken);
            var isNew = existing is null;

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            if (isNew)
                await bus.PublishAsync(new AeroEvent<DocViewModel>.DocCreated(ToViewModel(page), $"Doc created: {page.Slug}"));
            else
                await bus.PublishAsync(new AeroEvent<DocViewModel>.DocUpdated(ToViewModel(page), $"Doc updated: {page.Slug}"));

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
            var page = await session.LoadAsync<DocsPage>(id, cancellationToken);

            session.Delete<DocsPage>(id);
            await session.SaveChangesAsync(cancellationToken);

            if (page is not null)
                await bus.PublishAsync(new AeroEvent<DocViewModel>.DocDeleted(ToViewModel(page), $"Doc deleted: {page.Slug}"));

            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    private static DocViewModel ToViewModel(DocsPage page) => new()
    {
        Id = page.Id,
        Slug = page.Slug,
        Title = page.Title,
        Summary = page.Summary,
        MarkdownContent = page.MarkdownContent,
        SeoTitle = page.SeoTitle,
        SeoDescription = page.SeoDescription,
        PublicationState = page.PublicationState,
        PublishedOn = page.PublishedOn,
        ShowHeaderNavigation = page.ShowHeaderNavigation,
        HeaderImageUrl = page.HeaderImageUrl,
        ParentId = page.ParentId,
        Order = page.Order,
        CreatedOn = page.CreatedOn,
        ModifiedOn = page.ModifiedOn,
        CreatedBy = page.CreatedBy,
        ModifiedBy = page.ModifiedBy
    };

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
