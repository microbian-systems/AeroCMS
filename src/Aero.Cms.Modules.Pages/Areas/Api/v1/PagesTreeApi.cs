using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core.Http;
using Aero.Cms.Abstractions.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Pages.Areas.Api.v1;

/// <summary>
/// Admin API for page hierarchy / tree operations.
/// Registered as extension method on IEndpointRouteBuilder.
/// </summary>
public static class PagesTreeApi
{
        /// <summary>
    /// MapPagesTreeApi method.
    /// </summary>
public static void MapPagesTreeApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/pages/tree")
            .WithTags("Admin - Pages Tree");

        group.MapGet("/", GetTree)
            .WithName("GetPageTree");

        group.MapGet("/children", GetChildren)
            .WithName("GetPageChildren");

        group.MapGet("/translation-groups/children", GetTranslationGroupChildren)
            .WithName("GetPageTranslationGroupChildren");

        group.MapGet("/navigation", GetNavigation)
            .WithName("GetNavigationTree");

        group.MapGet("/breadcrumb/{id:long}", GetBreadcrumb)
            .WithName("GetBreadcrumb");

        group.MapGet("/ancestors/{id:long}", GetAncestors)
            .WithName("GetAncestors");

        group.MapPut("/{id:long}/move", MovePage)
            .WithName("MovePage");

        group.MapPost("/compute-path", ComputePath)
            .WithName("ComputePath");

        group.MapGet("/next-order", GetNextOrder)
            .WithName("GetNextOrder");
    }

    private static async Task<IResult> GetTree(
        [FromServices] IPageTreeService treeService,
        [FromServices] IQuerySession query,
        CancellationToken ct)
    {
        var result = await treeService.GetTreeAsync(ct);
        return result switch
        {
            Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok =>
                Results.Ok(await MapToTreeItemsAsync(query, ok.Value, ct)),
            _ => ToApiResult(result)
        };
    }

    private static async Task<IResult> GetChildren(
        [FromServices] IPageTreeService treeService,
        [FromServices] IQuerySession query,
        [FromQuery] long? parentId,
        CancellationToken ct)
    {
        var result = await treeService.GetChildrenAsync(parentId, ct);
        return result switch
        {
            Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok =>
                Results.Ok(await MapToTreeItemsAsync(query, ok.Value, ct)),
            _ => ToApiResult(result)
        };
    }

    private static async Task<IResult> GetTranslationGroupChildren(
        [FromServices] IQuerySession query,
        [FromServices] ISiteContext siteContext,
        [FromQuery] long? parentTranslationGroupId,
        [FromQuery] string? culture,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var site = await query.LoadAsync<SitesModel>(siteContext.SiteId, ct);
        var defaultCulture = ContentSlugDocument.NormalizeCulture(site?.DefaultCulture ?? SitesModel.DefaultCultureName);
        var selectedCulture = string.IsNullOrWhiteSpace(culture)
            ? defaultCulture
            : ContentSlugDocument.NormalizeCulture(culture);

        var pagesQuery = query.Query<PageDocument>()
            .Where(x => x.SiteId == siteContext.SiteId && x.Deleted == false);

        var pages = await pagesQuery.ToListAsync(ct);
        var allowedGroupIds = ResolveSearchGroupIds(pages, search);

        var items = MapToTranslationGroupTreeItems(
            pages,
            parentTranslationGroupId,
            defaultCulture,
            selectedCulture,
            allowedGroupIds);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetNavigation(
        [FromServices] INavigationService navService,
        CancellationToken ct)
    {
        var result = await navService.GetNavigationTreeAsync(ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> GetBreadcrumb(
        [FromServices] INavigationService navService,
        [FromRoute] long id,
        CancellationToken ct)
    {
        var result = await navService.GetBreadcrumbAsync(id, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> GetAncestors(
        [FromServices] IPageTreeService treeService,
        [FromRoute] long id,
        CancellationToken ct)
    {
        var result = await treeService.GetAncestorsAsync(id, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> MovePage(
        [FromServices] IPageTreeService treeService,
        [FromRoute] long id,
        [FromQuery] long? newParentId,
        [FromQuery] int? order,
        [FromQuery] PreviousPathBehavior? previousPathBehavior,
        CancellationToken ct)
    {
        var result = await treeService.MoveAsync(
            id,
            newParentId,
            order,
            previousPathBehavior,
            ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> ComputePath(
        [FromServices] IPageTreeService treeService,
        [FromServices] ISiteContext siteContext,
        [FromQuery] long? parentId,
        [FromQuery] string slug,
        [FromQuery] long? excludePageId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest("Slug is required.");

        var result = await treeService.ComputePathAsync(siteContext.SiteId, parentId, slug, excludePageId, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> GetNextOrder(
        [FromServices] IPageTreeService treeService,
        [FromServices] ISiteContext siteContext,
        [FromQuery] long? parentId,
        CancellationToken ct)
    {
        var result = await treeService.GetNextSiblingOrderAsync(siteContext.SiteId, parentId, ct);
        return ToApiResult(result);
    }

    /// <summary>
    /// Converts a Result&lt;T, AeroError&gt; to an IResult for minimal API responses.
    /// </summary>
    private static IResult ToApiResult<T>(Result<T, AeroError> result)
    {
        return result switch
        {
            Result<T, AeroError>.Ok ok => Results.Ok(ok.Value),
            Result<T, AeroError>.Failure failure => failure.Error switch
            {
                AeroError.NotFound => Results.NotFound(failure.Error.ToString()),
                AeroError.Conflict => Results.Conflict(failure.Error.ToString()),
                AeroError.Validation => Results.BadRequest(failure.Error.ToString()),
                _ => Results.Problem(failure.Error.ToString(), statusCode: 500)
            },
            _ => Results.Problem("Unknown result state.")
        };
    }

    /// <summary>
    /// Maps a list of PageDocuments to tree item DTOs, resolving the
    /// <c>hasChildren</c> flag via a single batch query.
    /// </summary>
    private static async Task<List<object>> MapToTreeItemsAsync(
        IQuerySession query,
        IReadOnlyList<PageDocument> pages,
        CancellationToken ct)
    {
        var pageIds = pages.Select(p => p.Id).ToList();
        if (pageIds.Count == 0)
            return [];

        // Single batch query: find which IDs are parents of non-deleted pages
        var parentIds = await query.Query<PageDocument>()
            .Where(x => x.ParentId.HasValue
                && pageIds.Contains(x.ParentId!.Value)
                && x.Deleted == false)
            .Select(x => x.ParentId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var parentSet = parentIds.ToHashSet();

        return pages.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Slug,
            p.Path,
            p.Depth,
            p.Order,
            p.ParentId,
            PublicationState = p.PublicationState.ToString(),
            p.IsHidden,
            HasChildren = parentSet.Contains(p.Id)
        }).ToList();
    }

    private static IReadOnlyList<PageTranslationGroupTreeItem> MapToTranslationGroupTreeItems(
        IReadOnlyList<PageDocument> pages,
        long? parentTranslationGroupId,
        string defaultCulture,
        string selectedCulture,
        IReadOnlySet<long>? allowedGroupIds)
    {
        if (pages.Count == 0)
        {
            return [];
        }

        var pageById = pages.ToDictionary(x => x.Id);
        var groups = pages
            .GroupBy(x => x.TranslationGroupId ?? x.Id)
            .ToDictionary(x => x.Key, x => x.OrderBy(p => p.Order).ThenBy(p => p.Culture).ToList());

        var groupParentIds = groups.ToDictionary(
            x => x.Key,
            x => ResolveParentTranslationGroupId(x.Value, pageById));

        var parentSet = groupParentIds.Values
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();

        return groups
            .Select(x => CreateTranslationGroupTreeItem(
                x.Key,
                x.Value,
                groupParentIds[x.Key],
                parentSet.Contains(x.Key),
                defaultCulture,
                selectedCulture))
            .Where(x => x.ParentTranslationGroupId == parentTranslationGroupId)
            .Where(x => allowedGroupIds is null || allowedGroupIds.Contains(x.TranslationGroupId))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static long? ResolveParentTranslationGroupId(
        IReadOnlyList<PageDocument> variants,
        IReadOnlyDictionary<long, PageDocument> pageById)
    {
        foreach (var variant in variants.OrderBy(x => x.Culture))
        {
            if (variant.ParentId is not { } parentId)
            {
                continue;
            }

            if (pageById.TryGetValue(parentId, out var parent))
            {
                return parent.TranslationGroupId ?? parent.Id;
            }
        }

        return null;
    }

    private static PageTranslationGroupTreeItem CreateTranslationGroupTreeItem(
        long translationGroupId,
        IReadOnlyList<PageDocument> variants,
        long? parentTranslationGroupId,
        bool hasChildren,
        string defaultCulture,
        string selectedCulture)
    {
        var defaultVariant = variants.FirstOrDefault(x => CultureEquals(x.Culture, defaultCulture));
        var selectedVariant = variants.FirstOrDefault(x => CultureEquals(x.Culture, selectedCulture));
        var display = selectedVariant ?? defaultVariant ?? variants.OrderBy(x => x.Culture).First();

        return new PageTranslationGroupTreeItem(
            translationGroupId,
            display.Id,
            display.Culture,
            defaultCulture,
            display.Title,
            display.Slug,
            display.Path,
            display.Depth,
            display.Order,
            parentTranslationGroupId,
            display.PublicationState.ToString(),
            display.IsHidden,
            hasChildren,
            defaultVariant is null,
            selectedVariant is null,
            variants
                .OrderByDescending(x => CultureEquals(x.Culture, defaultCulture))
                .ThenBy(x => x.Culture, StringComparer.OrdinalIgnoreCase)
                .Select(x => new PageTranslationVariantItem(
                    x.Id,
                    x.Culture,
                    x.Title,
                    x.Slug,
                    x.Path,
                    x.PublicationState.ToString(),
                    x.IsHidden,
                    CultureEquals(x.Culture, defaultCulture)))
                .ToList());
    }

    private static bool CultureEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlySet<long>? ResolveSearchGroupIds(IReadOnlyList<PageDocument> pages, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var needle = search.Trim();
        var pageById = pages.ToDictionary(x => x.Id);
        var groups = new HashSet<long>();

        foreach (var page in pages.Where(page => MatchesSearch(page, needle)))
        {
            groups.Add(page.TranslationGroupId ?? page.Id);

            var current = page;
            while (current.ParentId is { } parentId && pageById.TryGetValue(parentId, out var parent))
            {
                groups.Add(parent.TranslationGroupId ?? parent.Id);
                current = parent;
            }
        }

        return groups;
    }

    private static bool MatchesSearch(PageDocument page, string search)
        => page.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
            || page.Slug.Contains(search, StringComparison.OrdinalIgnoreCase)
            || page.Path.Contains(search, StringComparison.OrdinalIgnoreCase)
            || page.Culture.Contains(search, StringComparison.OrdinalIgnoreCase);
}
