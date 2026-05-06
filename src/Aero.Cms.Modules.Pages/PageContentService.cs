
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core;
using Aero.Core.Extensions;
using Wolverine;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Http;


namespace Aero.Cms.Modules.Pages;

public interface IPageContentService
{
    Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MartenPageContentService(IDocumentSession session, IBlockService blockService, IMessageBus bus, ISiteContext siteContext, IHttpContextAccessor? httpContextAccessor = null) : IPageContentService
{
    private readonly ISiteContext _siteContext = siteContext;
    public async Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await session.LoadAsync<PageDocument>(id, cancellationToken);
            return document is null || document.SiteId != _siteContext.SiteId
                ? Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError($"Page with id '{id}' not found or access denied"))
                : Prelude.Ok<PageDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("/", cancellationToken);

    public Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("blog", cancellationToken);

    public async Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = session.Query<PageDocument>().Where(x => x.SiteId == _siteContext.SiteId);

            IQueryable<PageDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }
            var stats = new global::Marten.Linq.QueryStatistics();
            var pages = await ((global::Marten.Linq.IMartenQueryable<PageDocument>)filteredQuery)
                .OrderBy(x => x.Title)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>((pages, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == _siteContext.SiteId &&
                    string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: cancellationToken);
            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.Page)
            {
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError($"Page with slug '{slug}' not found"));
            }

            var document = await session.LoadAsync<PageDocument>(reservation.OwnerId, cancellationToken);
            return document is null
                ? Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError($"Page with id '{reservation.OwnerId}' not found"))
                : Prelude.Ok<PageDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = _siteContext.SiteId,
            Title = request.Title,
            Slug = string.IsNullOrEmpty(request.Slug)
                ? request.Title.GenerateSlug()
                : request.Slug,
            Summary = request.Summary,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            PublicationState = request.PublicationState,
            ShowInNavMenu = request.ShowInNavMenu,
            ShowHeaderNavigation = request.ShowHeaderNavigation,
            HideFooter = request.HideFooter,
            ShowChatAgent = request.ShowChatAgent
        };

        if (request.EditorBlocks is { Count: > 0 })
        {
            page.Blocks = request.EditorBlocks.ToList();
            page.LayoutRegions = await MapEditorBlocksToLayoutRegions(request.EditorBlocks, cancellationToken);
        }
        else
        {
            page.Blocks = new List<EditorBlock>();
            page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
        }

        return await SaveAsync(page, cancellationToken);
    }

    public async Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAsync(id, cancellationToken);
        if (loadResult is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
        {
            var page = ok.Value;
            page.Title = request.Title;
            page.Slug = request.Slug;
            page.Summary = request.Summary;
            page.SeoTitle = request.SeoTitle;
            page.SeoDescription = request.SeoDescription;
            page.PublicationState = request.PublicationState;
            page.ShowInNavMenu = request.ShowInNavMenu;
            page.ShowHeaderNavigation = request.ShowHeaderNavigation;
            page.HideFooter = request.HideFooter;
            page.ShowChatAgent = request.ShowChatAgent;
            if (request.EditorBlocks is { Count: > 0 })
            {
                page.Blocks = request.EditorBlocks.ToList();
                page.LayoutRegions = await MapEditorBlocksToLayoutRegions(request.EditorBlocks, cancellationToken);
            }
            else
            {
                page.Blocks = new List<EditorBlock>();
                page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
            }

            return await SaveAsync(page, cancellationToken);
        }

        return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError($"Page with id '{id}' not found"));
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.CreateError($"Page with id '{id}' not found or access denied"));

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page && x.SiteId == _siteContext.SiteId, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<PageDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(page);
            await ValidatePage(page);

            var existingPage = await session.LoadAsync<PageDocument>(page.Id, cancellationToken);
            // Only stamp SiteId from context when not already set by the caller (e.g. seed).
            if (existingPage is null && page.SiteId == 0)
                page.SiteId = _siteContext.SiteId;
            await ContentSlugReservation.ReserveAsync(
                session,
                page.Id,
                ContentSlugOwnerType.Page,
                page.Slug,
                page.SiteId,
                existingPage?.Slug,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedOn = existingPage?.CreatedOn;
            page.CreatedOn = existingCreatedOn is null || existingCreatedOn == default ? now : existingCreatedOn.Value;
            page.ModifiedOn = now;
            page.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            page.PublishedOn = page.PublicationState == ContentPublicationState.Published
                ? existingPage?.PublishedOn ?? now
                : null;

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            if (page.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(page.Id, "Page", page.Slug, existingPage?.Slug));
            }

            return Prelude.Ok<PageDocument, AeroError>(page);

        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    private async Task<List<LayoutRegion>> MapEditorBlocksToLayoutRegions(IReadOnlyList<EditorBlock> editorBlocks, CancellationToken cancellationToken)
    {
        var placements = new List<BlockPlacement>();
        int order = 0;

        foreach (var eb in editorBlocks)
        {
            var block = EditorBlockMapper.MapBlock(eb);
            if (block != null)
            {
                await blockService.SaveAsync(block, cancellationToken);
                placements.Add(new BlockPlacement
                {
                    BlockId = block.Id,
                    Order = order++
                });
            }
        }

        // For now, put all editor blocks in a single column in one "Main" region
        var column = new LayoutColumn
        {
            Width = 12, // full width
            Blocks = placements
        };

        return [
            new LayoutRegion
            {
                Name = "Main",
                Order = 0,
                Columns = [column]
            }
        ];
    }

    private static async Task ValidatePage(PageDocument page)
    {
        var validator = new PageDocumentValidator();
        var valid = await validator.ValidateAsync(page);

        if (valid.Errors.Any())
        {
            throw new ArgumentException($"page errors: {string.Join(", ", valid.Errors.Select(e => e.ErrorMessage))}");
        }
    }
}
