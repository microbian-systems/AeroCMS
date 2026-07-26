using System.Collections.Immutable;
using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Web.Areas.Api.V1;

/// <summary>
/// Sable-backed public query facade. Every read is site-scoped, culture-scoped,
/// published-only, eagerly materialized, and bounded before leaving this service.
/// </summary>
public sealed class PublicCmsQueryService(
    IDocumentSession session,
    ISiteContext siteContext,
    IContentTypeService contentTypeService,
    IContentHierarchyQueryService contentHierarchyQueryService,
    IContentQueryService contentQueryService,
    ILogger<PublicCmsQueryService> logger) : IPublicCmsQueryService
{
    public const int MaximumTake = 50;
    public const int MaximumSkip = 10_000;
    public const int MaximumHierarchyDepth = 8;
    public const int MaximumHierarchyItems = 100;
    public const int MaximumProjectionFields = 32;

    public async Task<Result<PublicContentSearchResult>> QueryContentSearchAsync(
        string contentTypeAlias,
        string query,
        ContentSearchMode mode,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(contentTypeAlias))
            errors.Add("A content type alias is required.");
        if (string.IsNullOrWhiteSpace(query))
            errors.Add("Search text is required.");
        if (query?.Length > ContentSearchConstants.MaximumQueryLength)
            errors.Add($"Search text cannot exceed {ContentSearchConstants.MaximumQueryLength} characters.");
        if (skip is < 0 or > MaximumSkip)
            errors.Add($"Skip must be between 0 and {MaximumSkip}.");
        if (take is < 1 or > MaximumTake)
            errors.Add($"Take must be between 1 and {MaximumTake}.");
        if (errors.Count > 0)
            return AeroError.ValidationError(errors);

        var normalizedQuery = query?.Trim() ?? string.Empty;

        try
        {
            var siteId = RequireSiteId();
            var normalizedAlias = contentTypeAlias.Trim();
            var contentTypeResult = await contentTypeService.GetByAliasAsync(
                siteId,
                normalizedAlias,
                cancellationToken);
            if (contentTypeResult is not Result<ContentTypeDefinition, AeroError>.Ok contentTypeSuccess)
            {
                return AeroError.NotFoundError(
                    $"Content type '{contentTypeAlias}' was not found.");
            }

            if (contentTypeSuccess.Value.HideFromSearch)
            {
                return new PublicContentSearchResult(
                    [],
                    skip,
                    take,
                    HasMore: false);
            }

            var result = await contentQueryService.SearchIndexAsync(
                new ContentSearchRequest(
                    siteId,
                    normalizedAlias,
                    normalizedQuery,
                    ResolveCulture(),
                    mode,
                    PublishedOnly: true,
                    skip,
                    take,
                    new Dictionary<string, string>()),
                cancellationToken);
            return result switch
            {
                Result<ContentSearchResult>.Ok success =>
                    new PublicContentSearchResult(
                        success.Value.Items.Select(item => new PublicContentSearchItem(
                            FormatId(item.Id),
                            item.Title ?? string.Empty,
                            item.Slug,
                            $"/content/{normalizedAlias.Trim('/')}/{item.Slug.Trim('/')}",
                            item.Culture,
                            item.PublishedOn)).ToArray(),
                        skip,
                        take,
                        success.Value.HasMore),
                Result<ContentSearchResult>.Failure failure => failure.Error,
                _ => AeroError.CreateError("Unexpected content search result.")
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Published content search failed for {ContentTypeAlias}.",
                contentTypeAlias);
            return AeroError.DatabaseError("The published content search failed.");
        }
    }

    public async Task<Result<PublicQueryPage<PublicPageQueryItem>>> QueryPagesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePage(skip, take);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var siteId = RequireSiteId();
            var culture = ResolveCulture();
            var statistics = new QueryStatistics();
            var documents = await session.Query<PageDocument>()
                .Where(page =>
                    page.SiteId == siteId
                    && page.Culture == culture
                    && page.PublicationState == ContentPublicationState.Published
                    && !page.Deleted)
                .OrderBy(page => page.Order)
                .ThenBy(page => page.Id)
                .Stats(out statistics)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return new PublicQueryPage<PublicPageQueryItem>(
                documents.Select(page => new PublicPageQueryItem(
                    FormatId(page.Id),
                    page.Title,
                    page.Slug,
                    page.Path,
                    page.Summary,
                    page.Culture,
                    page.PublishedOn)).ToArray(),
                statistics.TotalResults,
                skip,
                take);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Published page query failed.");
            return AeroError.DatabaseError("The published page query failed.");
        }
    }

    public async Task<Result<PublicQueryPage<PublicPostQueryItem>>> QueryPostsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePage(skip, take);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var siteId = RequireSiteId();
            var culture = ResolveCulture();
            var statistics = new QueryStatistics();
            var documents = await session.Query<PostDocument>()
                .Where(post =>
                    post.SiteId == siteId
                    && post.Culture == culture
                    && post.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(post => post.PublishedOn)
                .ThenByDescending(post => post.Id)
                .Stats(out statistics)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return new PublicQueryPage<PublicPostQueryItem>(
                documents.Select(post => new PublicPostQueryItem(
                    FormatId(post.Id),
                    post.Title,
                    post.Slug,
                    post.Excerpt,
                    post.Culture,
                    post.ImageUrl,
                    post.PublishedOn)).ToArray(),
                statistics.TotalResults,
                skip,
                take);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Published post query failed.");
            return AeroError.DatabaseError("The published post query failed.");
        }
    }

    public async Task<Result<PublicQueryPage<PublicDocsQueryItem>>> QueryDocsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePage(skip, take);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var siteId = RequireSiteId();
            var culture = ResolveCulture();
            var statistics = new QueryStatistics();
            var documents = await session.Query<DocsPage>()
                .Where(document =>
                    document.SiteId == siteId
                    && document.Culture == culture
                    && document.PublicationState == ContentPublicationState.Published)
                .OrderBy(document => document.Order)
                .ThenBy(document => document.Id)
                .Stats(out statistics)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return new PublicQueryPage<PublicDocsQueryItem>(
                documents.Select(document => new PublicDocsQueryItem(
                    FormatId(document.Id),
                    document.Title,
                    document.Slug,
                    document.Summary,
                    document.Culture,
                    document.ParentId is null ? null : FormatId(document.ParentId.Value),
                    document.Order,
                    document.PublishedOn)).ToArray(),
                statistics.TotalResults,
                skip,
                take);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Published documentation query failed.");
            return AeroError.DatabaseError("The published documentation query failed.");
        }
    }

    public async Task<Result<ContentQueryResult>> QueryContentAsync(
        string contentTypeAlias,
        ContentTraversal traversal,
        string? rootId,
        int maximumDepth,
        int maximumItems,
        IReadOnlyList<string>? projection,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateContent(
            contentTypeAlias,
            rootId,
            maximumDepth,
            maximumItems,
            projection,
            out var parsedRootId);
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var siteId = RequireSiteId();
            var contentTypeResult = await contentTypeService.GetByAliasAsync(
                siteId,
                contentTypeAlias.Trim(),
                cancellationToken);
            if (contentTypeResult is not Result<ContentTypeDefinition, AeroError>.Ok contentTypeSuccess)
            {
                return AeroError.NotFoundError(
                    $"Content type '{contentTypeAlias}' was not found.");
            }

            var contentType = contentTypeSuccess.Value;
            return await contentHierarchyQueryService.QueryAsync(
                new ContentQueryRequest(
                    contentType.Alias,
                    siteId,
                    contentType.Id,
                    contentType.Alias,
                    ResolveCulture(),
                    traversal,
                    parsedRootId,
                    maximumDepth,
                    maximumItems,
                    (projection ?? [])
                        .Select(field => field.Trim())
                        .Where(field => field.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToImmutableArray(),
                    IncludeDrafts: false),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Published content query failed for {ContentTypeAlias}.",
                contentTypeAlias);
            return AeroError.DatabaseError("The published content query failed.");
        }
    }

    private long RequireSiteId()
        => siteContext.SiteId > 0
            ? siteContext.SiteId
            : throw new InvalidOperationException("No public site was resolved for this request.");

    private static string ResolveCulture()
    {
        var culture = CultureInfo.CurrentUICulture;
        return string.IsNullOrWhiteSpace(culture.Name)
            ? "en-US"
            : culture.Name;
    }

    private static string FormatId(long id)
        => id.ToString(CultureInfo.InvariantCulture);

    private static AeroError.Validation? ValidatePage(int skip, int take)
    {
        var errors = new List<string>();
        if (skip is < 0 or > MaximumSkip)
        {
            errors.Add($"skip must be between 0 and {MaximumSkip}.");
        }

        if (take is < 1 or > MaximumTake)
        {
            errors.Add($"take must be between 1 and {MaximumTake}.");
        }

        return errors.Count == 0 ? null : AeroError.ValidationError(errors);
    }

    private static AeroError.Validation? ValidateContent(
        string contentTypeAlias,
        string? rootId,
        int maximumDepth,
        int maximumItems,
        IReadOnlyList<string>? projection,
        out long? parsedRootId)
    {
        parsedRootId = null;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(contentTypeAlias)
            || contentTypeAlias.Length > 128)
        {
            errors.Add("contentTypeAlias is required and cannot exceed 128 characters.");
        }

        if (maximumDepth is < 1 or > MaximumHierarchyDepth)
        {
            errors.Add($"maximumDepth must be between 1 and {MaximumHierarchyDepth}.");
        }

        if (maximumItems is < 1 or > MaximumHierarchyItems)
        {
            errors.Add($"maximumItems must be between 1 and {MaximumHierarchyItems}.");
        }

        if (projection is { Count: > MaximumProjectionFields })
        {
            errors.Add($"projection cannot contain more than {MaximumProjectionFields} fields.");
        }

        if (!string.IsNullOrWhiteSpace(rootId)
            && (!long.TryParse(
                    rootId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value)
                || value <= 0))
        {
            errors.Add("rootId must be a positive decimal Snowflake identifier.");
        }
        else if (!string.IsNullOrWhiteSpace(rootId))
        {
            parsedRootId = long.Parse(rootId, CultureInfo.InvariantCulture);
        }

        return errors.Count == 0 ? null : AeroError.ValidationError(errors);
    }
}
