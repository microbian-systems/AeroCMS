using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Search;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>Implements bounded, site-scoped content queries with Sable projections.</summary>
public sealed class AeroContentQueryService(
    IDocumentSession session,
    IContentEmbeddingGenerator? embeddingGenerator = null) : IContentQueryService
{
    private readonly IContentEmbeddingGenerator embeddingGenerator =
        embeddingGenerator ?? new UnavailableContentEmbeddingGenerator();

    public async Task<Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>> GetByTypeAsync(
        long siteId, string alias, int skip, int take, CancellationToken ct)
    {
        var query = session.Query<ContentItem>()
            .Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.PublishedOn)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        await HydrateSharedFieldsAsync(items, ct);
        return Prelude.Ok<(IReadOnlyList<ContentItem>, long), AeroError>((items, total));
    }

    public async Task<Result<long, AeroError>> CountByTypeAsync(
        long siteId, string alias, CancellationToken ct = default)
    {
        var count = await session.Query<ContentItem>()
            .Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias)
            .CountAsync(ct);
        return Prelude.Ok<long, AeroError>(count);
    }

    public async Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId,
        string alias,
        Dictionary<string, string> filters,
        CancellationToken ct)
    {
        filters.TryGetValue("__culture", out var culture);
        filters.TryGetValue("__search", out var search);
        var exact = filters
            .Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var result = await SearchIndexAsync(
            new ContentSearchRequest(
                siteId,
                alias,
                search ?? string.Empty,
                culture,
                ContentSearchMode.FullText,
                PublishedOnly: false,
                Skip: 0,
                Take: ContentSearchConstants.MaximumInternalTake,
                ExactFilters: exact),
            ct);

        return result switch
        {
            Result<ContentSearchResult>.Ok success =>
                Prelude.Ok<IReadOnlyList<ContentItem>, AeroError>(success.Value.Items),
            Result<ContentSearchResult>.Failure failure =>
                Prelude.Fail<IReadOnlyList<ContentItem>, AeroError>(failure.Error),
            _ => Prelude.Fail<IReadOnlyList<ContentItem>, AeroError>(
                AeroError.CreateError("Unexpected content search result."))
        };
    }

    public async Task<Result<ContentSearchResult>> SearchIndexAsync(
        ContentSearchRequest request,
        CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return validation;
        }

        var rankedIds = await QueryRankedIdsAsync(request, ct);
        if (rankedIds is Result<IReadOnlyList<long>?>.Failure rankedFailure)
        {
            return rankedFailure.Error;
        }

        var exactIds = await QueryExactIdsAsync(request, ct);
        IReadOnlyList<long>? candidates =
            (rankedIds as Result<IReadOnlyList<long>?>.Ok)?.Value;
        var preserveRankedOrder = candidates is not null;

        if (exactIds is not null)
        {
            candidates = candidates is null
                ? exactIds.ToArray()
                : candidates.Where(exactIds.Contains).ToArray();
        }

        if (candidates is null)
        {
            return await QueryItemsDirectlyAsync(request, ct);
        }

        if (!preserveRankedOrder)
        {
            var exactMatches = await session.LoadManyAsync<ContentItem>(
                candidates.Take(ContentSearchConstants.MaximumCandidates),
                ct);
            var orderedExactMatches = (await HydrateSearchResultsAsync(exactMatches, request, ct))
                .OrderByDescending(item => item.PublishedOn)
                .ThenBy(item => item.Id)
                .ToArray();
            return new ContentSearchResult(
                orderedExactMatches
                    .Skip(request.Skip)
                    .Take(request.Take)
                    .ToArray(),
                orderedExactMatches.Length > request.Skip + request.Take);
        }

        var pageIds = candidates
            .Skip(request.Skip)
            .Take(request.Take + 1)
            .ToArray();
        var hasMore = pageIds.Length > request.Take;
        pageIds = pageIds.Take(request.Take).ToArray();
        var loaded = await session.LoadManyAsync<ContentItem>(pageIds, ct);
        var hydrated = await HydrateSearchResultsAsync(loaded, request, ct);
        var byId = hydrated.ToDictionary(item => item.Id);
        var ordered = pageIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
        return new ContentSearchResult(ordered, hasMore);
    }

    public async Task<Result<IReadOnlyList<ContentItem>, AeroError>> ListCultureVariantsAsync(
        long siteId, string alias, long translationGroupId, CancellationToken ct = default)
    {
        var items = await session.Query<ContentItem>()
            .Where(x =>
                x.SiteId == siteId
                && x.ContentTypeAlias == alias
                && (x.TranslationGroupId == translationGroupId || x.Id == translationGroupId))
            .OrderBy(x => x.Culture)
            .ToListAsync(ct);
        await HydrateSharedFieldsAsync(items, ct);
        return Prelude.Ok<IReadOnlyList<ContentItem>, AeroError>(items);
    }

    private async Task<Result<IReadOnlyList<long>?>> QueryRankedIdsAsync(
        ContentSearchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new Result<IReadOnlyList<long>?>.Ok(null);
        }

        var siteId = request.SiteId;
        var alias = request.ContentTypeAlias;
        var culture = request.Culture;
        var publishedOnly = request.PublishedOnly;

        if (request.Mode == ContentSearchMode.Semantic)
        {
            if (!embeddingGenerator.IsAvailable)
            {
                return AeroError.ValidationError(
                    ["Semantic content search requires a configured embedding generator."]);
            }

            if (embeddingGenerator.Dimensions != ContentSearchConstants.VectorDimensions)
            {
                return AeroError.ValidationError(
                    [$"The configured embedding generator must emit {ContentSearchConstants.VectorDimensions} dimensions."]);
            }

            var generated = await embeddingGenerator.GenerateAsync(request.Query.Trim(), ct);
            if (generated is not Result<float[]>.Ok embedding)
            {
                return generated is Result<float[]>.Failure failure
                    ? failure.Error
                    : AeroError.CreateError("The embedding generator returned an unexpected result.");
            }

            if (embedding.Value.Length != ContentSearchConstants.VectorDimensions)
            {
                return AeroError.ValidationError(
                    [$"The embedding generator returned {embedding.Value.Length} dimensions; " +
                     $"{ContentSearchConstants.VectorDimensions} are required."]);
            }

            var modelId = embeddingGenerator.ModelId;
            var embeddingDimensions = embeddingGenerator.Dimensions;
            var semanticSearch = session.Search<ContentSemanticDocument>()
                .WithVector(document => document.Embedding, embedding.Value);
            semanticSearch = ApplySemanticScope(
                semanticSearch,
                siteId,
                alias,
                culture,
                publishedOnly,
                modelId,
                embeddingDimensions);
            var results = await semanticSearch
                .Candidates(ContentSearchConstants.MaximumCandidates)
                .Take(ContentSearchConstants.MaximumCandidates)
                .ToListAsync(ct);
            return new Result<IReadOnlyList<long>?>.Ok(
                results.Select(document => document.ContentItemId).ToArray());
        }

        var fullTextSearch = session.Search<ContentSearchDocument>()
            .MatchText(document => (object)document.FullText, request.Query.Trim());
        fullTextSearch = ApplyFullTextScope(
            fullTextSearch,
            siteId,
            alias,
            culture,
            publishedOnly);
        var fullTextResults = await fullTextSearch
            .Take(ContentSearchConstants.MaximumCandidates)
            .ToListAsync(ct);
        return new Result<IReadOnlyList<long>?>.Ok(
            fullTextResults.Select(document => document.ContentItemId).ToArray());
    }

    private async Task<HashSet<long>?> QueryExactIdsAsync(
        ContentSearchRequest request,
        CancellationToken ct)
    {
        HashSet<long>? intersection = null;
        foreach (var (fieldName, expectedValue) in request.ExactFilters)
        {
            var normalized = ContentIndexService.NormalizeExactValue(expectedValue);
            var siteId = request.SiteId;
            var alias = request.ContentTypeAlias;
            var culture = request.Culture;
            var publishedOnly = request.PublishedOnly;
            var query = session.Query<ContentSearchFacet>()
                .Where(facet =>
                    facet.SiteId == siteId
                    && facet.ContentTypeAlias == alias
                    && facet.FieldName == fieldName
                    && facet.NormalizedValue == normalized);
            if (culture is not null)
            {
                query = query.Where(facet => facet.Culture == culture);
            }
            if (publishedOnly)
            {
                query = query.Where(facet =>
                    facet.PublicationState == ContentPublicationState.Published
                    && facet.HideFromSearch == false);
            }

            var rows = await query
                .Take(ContentSearchConstants.MaximumCandidates)
                .ToListAsync(ct);
            var ids = rows.Select(row => row.ContentItemId).ToHashSet();
            intersection = intersection is null
                ? ids
                : intersection.Intersect(ids).ToHashSet();
            if (intersection.Count == 0)
            {
                break;
            }
        }

        return intersection;
    }

    private async Task<Result<ContentSearchResult>> QueryItemsDirectlyAsync(
        ContentSearchRequest request,
        CancellationToken ct)
    {
        var siteId = request.SiteId;
        var alias = request.ContentTypeAlias;
        var culture = request.Culture;
        var publishedOnly = request.PublishedOnly;
        var query = session.Query<ContentItem>()
            .Where(item =>
                item.SiteId == siteId
                && item.ContentTypeAlias == alias);
        if (culture is not null)
        {
            query = query.Where(item => item.Culture == culture);
        }
        if (publishedOnly)
        {
            query = query.Where(item =>
                item.PublicationState == ContentPublicationState.Published);
        }

        var items = await query
            .OrderByDescending(item => item.PublishedOn)
            .Skip(request.Skip)
            .Take(request.Take + 1)
            .ToListAsync(ct);
        var hydrated = await HydrateSearchResultsAsync(items, request, ct);
        return new ContentSearchResult(
            hydrated.Take(request.Take).ToArray(),
            hydrated.Count > request.Take);
    }

    private async Task<IReadOnlyList<ContentItem>> HydrateSearchResultsAsync(
        IEnumerable<ContentItem> items,
        ContentSearchRequest request,
        CancellationToken ct)
    {
        var candidates = items
            .Where(item => item.SiteId == request.SiteId
                && string.Equals(item.ContentTypeAlias, request.ContentTypeAlias, StringComparison.Ordinal)
                && (request.Culture is null || item.Culture == request.Culture)
                && (!request.PublishedOnly || item.PublicationState == ContentPublicationState.Published))
            .ToArray();
        if (candidates.Length == 0) return [];

        var groupIds = candidates
            .Where(item => item.TranslationGroupId is not null)
            .Select(item => item.TranslationGroupId!.Value)
            .Distinct()
            .ToArray();
        var groups = groupIds.Length == 0
            ? []
            : await session.LoadManyAsync<ContentTranslationGroupDocument>(groupIds, ct);
        var groupsById = groups
            .Where(group => group.SiteId == request.SiteId
                && string.Equals(group.ContentTypeAlias, request.ContentTypeAlias, StringComparison.Ordinal))
            .ToDictionary(group => group.Id);

        return candidates.Select(item =>
        {
            var copy = Clone(item);
            if (copy.TranslationGroupId is { } groupId
                && groupsById.TryGetValue(groupId, out var group))
            {
                foreach (var (name, value) in group.SharedFields)
                {
                    copy.Fields[name] = value.Clone();
                }
            }
            return copy;
        }).ToArray();
    }

    private async Task HydrateSharedFieldsAsync(IEnumerable<ContentItem> items, CancellationToken ct)
    {
        var materialized = items.ToArray();
        var groupIds = materialized
            .Where(item => item.TranslationGroupId is not null)
            .Select(item => item.TranslationGroupId!.Value)
            .Distinct()
            .ToArray();
        if (groupIds.Length == 0) return;

        var groups = await session.LoadManyAsync<ContentTranslationGroupDocument>(groupIds, ct);
        var byId = groups.ToDictionary(group => group.Id);
        foreach (var item in materialized)
        {
            if (item.TranslationGroupId is not { } groupId || !byId.TryGetValue(groupId, out var group)) continue;
            foreach (var (name, value) in group.SharedFields)
                item.Fields[name] = value.Clone();
        }
    }

    private static ContentItem Clone(ContentItem source) => new()
    {
        Id = source.Id, Version = source.Version, SiteId = source.SiteId,
        ContentTypeAlias = source.ContentTypeAlias, Slug = source.Slug, Title = source.Title,
        TranslationGroupId = source.TranslationGroupId, Culture = source.Culture, SourceItemId = source.SourceItemId,
        ParentId = source.ParentId, SortOrder = source.SortOrder,
        Fields = source.Fields.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), source.Fields.Comparer),
        PublicationState = source.PublicationState, PublishedOn = source.PublishedOn, VersionNumber = source.VersionNumber,
        SchedulePublishUtc = source.SchedulePublishUtc, ScheduleUnpublishUtc = source.ScheduleUnpublishUtc,
        CreatedOn = source.CreatedOn, ModifiedOn = source.ModifiedOn, CreatedBy = source.CreatedBy, ModifiedBy = source.ModifiedBy,
        TranslationProvenance = source.TranslationProvenance,
        TranslationReview = new(source.TranslationReview.Status, source.TranslationReview.ReviewedOn,
            source.TranslationReview.ReviewedBy, source.TranslationReview.Notes,
            source.TranslationReview.ReviewedSourceItemId, source.TranslationReview.ReviewedSourceVersionNumber,
            source.TranslationReview.ReviewedTargetVersionNumber)
    };

    private static AeroError.Validation? Validate(ContentSearchRequest request)
    {
        var errors = new List<string>();
        if (request.SiteId <= 0)
            errors.Add("A valid site is required.");
        if (string.IsNullOrWhiteSpace(request.ContentTypeAlias))
            errors.Add("A content type alias is required.");
        if (request.Query.Length > ContentSearchConstants.MaximumQueryLength)
            errors.Add($"Search text cannot exceed {ContentSearchConstants.MaximumQueryLength} characters.");
        if (request.Skip is < 0 or > ContentSearchConstants.MaximumSkip)
            errors.Add($"Skip must be between 0 and {ContentSearchConstants.MaximumSkip}.");
        if (request.Take is < 1 or > ContentSearchConstants.MaximumInternalTake)
            errors.Add($"Take must be between 1 and {ContentSearchConstants.MaximumInternalTake}.");
        return errors.Count == 0 ? null : AeroError.ValidationError(errors);
    }

    private static ISearchQuery<ContentSemanticDocument> ApplySemanticScope(
        ISearchQuery<ContentSemanticDocument> search,
        long siteId,
        string alias,
        string? culture,
        bool publishedOnly,
        string modelId,
        int embeddingDimensions)
    {
        if (culture is not null && publishedOnly)
        {
            return search.Where(document =>
                document.SiteId == siteId
                && document.ContentTypeAlias == alias
                && document.ModelId == modelId
                && document.EmbeddingDimensions == embeddingDimensions
                && document.Culture == culture
                && document.PublicationState == ContentPublicationState.Published
                && document.HideFromSearch == false);
        }

        if (culture is not null)
        {
            return search.Where(document =>
                document.SiteId == siteId
                && document.ContentTypeAlias == alias
                && document.ModelId == modelId
                && document.EmbeddingDimensions == embeddingDimensions
                && document.Culture == culture);
        }

        if (publishedOnly)
        {
            return search.Where(document =>
                document.SiteId == siteId
                && document.ContentTypeAlias == alias
                && document.ModelId == modelId
                && document.EmbeddingDimensions == embeddingDimensions
                && document.PublicationState == ContentPublicationState.Published
                && document.HideFromSearch == false);
        }

        return search.Where(document =>
            document.SiteId == siteId
            && document.ContentTypeAlias == alias
            && document.ModelId == modelId
            && document.EmbeddingDimensions == embeddingDimensions);
    }

    private static ISearchQuery<ContentSearchDocument> ApplyFullTextScope(
        ISearchQuery<ContentSearchDocument> search,
        long siteId,
        string alias,
        string? culture,
        bool publishedOnly)
    {
        if (culture is not null && publishedOnly)
        {
            return search.Where(document =>
                document.SiteId == siteId
                && document.ContentTypeAlias == alias
                && document.Culture == culture
                && document.PublicationState == ContentPublicationState.Published
                && document.HideFromSearch == false);
        }

        if (culture is not null)
        {
            return search.Where(document =>
                document.SiteId == siteId
                && document.ContentTypeAlias == alias
                && document.Culture == culture);
        }

        if (publishedOnly)
        {
            return search.Where(document =>
                document.SiteId == siteId
                && document.ContentTypeAlias == alias
                && document.PublicationState == ContentPublicationState.Published
                && document.HideFromSearch == false);
        }

        return search.Where(document =>
            document.SiteId == siteId
            && document.ContentTypeAlias == alias);
    }
}
