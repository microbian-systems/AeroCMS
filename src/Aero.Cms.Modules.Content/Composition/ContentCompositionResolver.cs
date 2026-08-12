using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Http;

namespace Aero.Cms.Modules.Content.Composition;

/// <summary>
/// Resolves published page-composition data through Content-owned services.
/// </summary>
/// <remarks>
/// Dynamic field filters currently run over a bounded candidate set because the underlying
/// provider does not expose safe dynamic JSON query translation. Exceeding the bound fails
/// closed instead of loading an unbounded content type into memory.
/// </remarks>
public sealed class ContentCompositionResolver : IContentCompositionResolver
{
    private readonly IContentTypeService contentTypes;
    private readonly IContentService contentItems;
    private readonly IContentQueryService contentQueries;
    private readonly IReadOnlyDictionary<string, IContentEntrySourceProvider> entryProviders;
    private readonly IContentEntrySourceProviderCatalog? entryProviderCatalog;
    private readonly ISiteContext? siteContext;

    public ContentCompositionResolver(
        IContentTypeService contentTypes,
        IContentService contentItems,
        IContentQueryService contentQueries)
    {
        this.contentTypes = contentTypes;
        this.contentItems = contentItems;
        this.contentQueries = contentQueries;
        entryProviders = new Dictionary<string, IContentEntrySourceProvider>(StringComparer.OrdinalIgnoreCase);
    }

    public ContentCompositionResolver(
        IContentTypeService contentTypes,
        IContentService contentItems,
        IContentQueryService contentQueries,
        IEnumerable<IContentEntrySourceProvider> entryProviders,
        ISiteContext siteContext)
        : this(contentTypes, contentItems, contentQueries, entryProviders, siteContext, null)
    {
    }

    public ContentCompositionResolver(
        IContentTypeService contentTypes,
        IContentService contentItems,
        IContentQueryService contentQueries,
        IEnumerable<IContentEntrySourceProvider> entryProviders,
        ISiteContext siteContext,
        IContentEntrySourceProviderCatalog? entryProviderCatalog)
    {
        this.contentTypes = contentTypes;
        this.contentItems = contentItems;
        this.contentQueries = contentQueries;
        this.siteContext = siteContext;
        this.entryProviderCatalog = entryProviderCatalog;
        this.entryProviders = entryProviders
            .GroupBy(provider => provider.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }
    /// <summary>Gets the maximum site/type candidate set evaluated in memory.</summary>
    public const int MaximumCandidateCount = 1_000;

    /// <inheritdoc />
    public async Task<Result<PublishedContentItemProjection, AeroError>> ResolveItemAsync(
        long siteId,
        string culture,
        PageContentItemScope scope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.ContentEntryKey is { } virtualKey)
        {
            return await ResolveVirtualItemAsync(siteId, scope, virtualKey, ct);
        }

        var input = ValidateInput(siteId, culture);
        if (input is Result<string, AeroError>.Failure inputFailure)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(inputFailure.Error);
        }

        var normalizedCulture = ((Result<string, AeroError>.Ok)input).Value;
        var definitionResult = await contentTypes.GetByIdAsync(siteId, scope.ContentTypeId, ct);
        if (definitionResult is Result<ContentTypeDefinition, AeroError>.Failure definitionFailure)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(definitionFailure.Error);
        }

        var definition = ((Result<ContentTypeDefinition, AeroError>.Ok)definitionResult).Value;
        var itemResult = scope.LookupMode switch
        {
            PageContentItemLookupMode.StableId when scope.ContentItemId is > 0 =>
                await contentItems.LoadAsync(siteId, scope.ContentItemId.Value, ct),
            PageContentItemLookupMode.Slug when !string.IsNullOrWhiteSpace(scope.Slug) =>
                await contentItems.GetBySlugAndTypeAsync(
                    siteId,
                    definition.Alias,
                    normalizedCulture,
                    scope.Slug.Trim(),
                    ct),
            _ => Prelude.Fail<ContentItem, AeroError>(
                AeroError.ValidationError([$"Content item scope '{scope.NodeId}' has an invalid lookup."]))
        };

        if (itemResult is Result<ContentItem, AeroError>.Failure itemFailure)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(itemFailure.Error);
        }

        var item = ((Result<ContentItem, AeroError>.Ok)itemResult).Value;
        var visibility = ValidatePublishedItem(item, definition.Alias, normalizedCulture, scope.NodeId);
        if (visibility is Result<bool, AeroError>.Failure visibilityFailure)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(visibilityFailure.Error);
        }

        return Prelude.Ok<PublishedContentItemProjection, AeroError>(CreateProjection(item));
    }

    private async Task<Result<PublishedContentItemProjection, AeroError>> ResolveVirtualItemAsync(
        long siteId,
        PageContentItemScope scope,
        ContentEntryKey key,
        CancellationToken ct)
    {
        if (!key.IsValid)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(
                AeroError.ValidationError([$"Content item scope '{scope.NodeId}' has an invalid virtual entry key."]));
        }

        if (siteContext is null || siteContext.SiteId != siteId || siteContext.TenantId <= 0)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(
                AeroError.ConfigurationError("A current tenant and site are required to resolve virtual page content."));
        }

        var currentScope = new ContentViewScope(siteContext.TenantId, siteContext.SiteId);
        var provider = entryProviders.GetValueOrDefault(key.Provider)
            ?? (entryProviderCatalog is null
                ? null
                : await entryProviderCatalog.ResolveAsync(currentScope, key.Provider, ct));
        if (provider is null)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(
                AeroError.NotFoundError($"Virtual content provider '{key.Provider}' for scope '{scope.NodeId}' was not found."));
        }

        var entry = await provider.FindAsync(currentScope, key.StableId, ct);
        if (entry is null
            || entry.Scope != currentScope
            || !string.Equals(entry.Key.Provider, key.Provider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entry.Key.StableId, key.StableId, StringComparison.Ordinal))
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(
                AeroError.NotFoundError($"Virtual content entry for scope '{scope.NodeId}' was not found."));
        }

        try
        {
            var fields = entry.Values.ToDictionary(
                pair => pair.Key,
                pair => pair.Value is JsonElement element
                    ? element.Clone()
                    : JsonSerializer.SerializeToElement(pair.Value),
                StringComparer.OrdinalIgnoreCase);
            return Prelude.Ok<PublishedContentItemProjection, AeroError>(new PublishedContentItemProjection
            {
                ContentTypeAlias = string.IsNullOrWhiteSpace(scope.ContentTypeAlias)
                    ? provider.Provider
                    : scope.ContentTypeAlias,
                Slug = key.StableId,
                Culture = string.Empty,
                Fields = fields
            });
        }
        catch (Exception)
        {
            return Prelude.Fail<PublishedContentItemProjection, AeroError>(
                AeroError.ValidationError([$"Virtual content entry for scope '{scope.NodeId}' contains values that cannot be rendered."]));
        }
    }

    /// <inheritdoc />
    public async Task<Result<PublishedContentPage, AeroError>> ResolveListAsync(
        long siteId,
        string culture,
        PageContentListScope scope,
        int pageNumber,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var input = ValidateInput(siteId, culture);
        if (input is Result<string, AeroError>.Failure inputFailure)
        {
            return Prelude.Fail<PublishedContentPage, AeroError>(inputFailure.Error);
        }

        var queryError = ValidateQuery(scope, pageNumber);
        if (queryError is not null)
        {
            return Prelude.Fail<PublishedContentPage, AeroError>(queryError);
        }

        if (!string.IsNullOrWhiteSpace(scope.ContentEntryProvider))
            return await ResolveVirtualListAsync(siteId, culture, scope, pageNumber, ct);

        var normalizedCulture = ((Result<string, AeroError>.Ok)input).Value;
        var definitionResult = await contentTypes.GetByIdAsync(siteId, scope.ContentTypeId, ct);
        if (definitionResult is Result<ContentTypeDefinition, AeroError>.Failure definitionFailure)
        {
            return Prelude.Fail<PublishedContentPage, AeroError>(definitionFailure.Error);
        }

        var definition = ((Result<ContentTypeDefinition, AeroError>.Ok)definitionResult).Value;
        var candidatesResult = await contentQueries.GetByTypeAsync(
            siteId,
            definition.Alias,
            skip: 0,
            take: MaximumCandidateCount + 1,
            ct);
        if (candidatesResult is Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>.Failure candidatesFailure)
        {
            return Prelude.Fail<PublishedContentPage, AeroError>(candidatesFailure.Error);
        }

        var candidates = ((Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>.Ok)candidatesResult).Value;
        if (candidates.TotalCount > MaximumCandidateCount || candidates.Items.Count > MaximumCandidateCount)
        {
            return Prelude.Fail<PublishedContentPage, AeroError>(
                AeroError.ValidationError([
                    $"Content list scope '{scope.NodeId}' exceeds the bounded render query limit of {MaximumCandidateCount} candidates."
                ]));
        }

        var filtered = candidates.Items
            .Where(item => item.PublicationState == ContentPublicationState.Published)
            .Where(item => string.Equals(item.Culture, normalizedCulture, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(item.ContentTypeAlias, definition.Alias, StringComparison.OrdinalIgnoreCase))
            .Where(item => (scope.Query.Filters ?? []).All(filter => MatchesFilter(item, filter)))
            .ToList();

        Sort(filtered, scope.Query);

        var totalCount = filtered.Count;
        var skip = ((long)pageNumber - 1L) * scope.Query.PageSize;
        var pageItems = skip >= filtered.Count
            ? []
            : filtered
                .Skip((int)skip)
                .Take(scope.Query.PageSize)
                .Select(CreateProjection)
                .ToArray();

        return Prelude.Ok<PublishedContentPage, AeroError>(new PublishedContentPage
        {
            ContentTypeAlias = definition.Alias,
            Items = pageItems,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = scope.Query.PageSize
        });
    }

    private async Task<Result<PublishedContentPage, AeroError>> ResolveVirtualListAsync(long siteId, string culture,
        PageContentListScope scope, int pageNumber, CancellationToken ct)
    {
        if (siteContext is null || siteContext.SiteId != siteId || siteContext.TenantId <= 0)
            return Prelude.Fail<PublishedContentPage, AeroError>(AeroError.ConfigurationError("A current tenant and site are required to resolve virtual page content."));
        var providerKey = scope.ContentEntryProvider!.Trim();
        if (providerKey.Length > 128)
            return Prelude.Fail<PublishedContentPage, AeroError>(AeroError.ValidationError(["Virtual content provider is too long."]));
        var viewScope = new ContentViewScope(siteContext.TenantId, siteContext.SiteId);
        var provider = entryProviders.GetValueOrDefault(providerKey)
            ?? (entryProviderCatalog is null ? null : await entryProviderCatalog.ResolveAsync(viewScope, providerKey, ct));
        if (provider is null)
            return Prelude.Fail<PublishedContentPage, AeroError>(AeroError.NotFoundError($"Virtual content provider '{providerKey}' was not found."));
        var offset = (long)(pageNumber - 1) * scope.Query.PageSize;
        var requested = offset + scope.Query.PageSize;
        if (offset < 0 || requested > 100)
            return Prelude.Fail<PublishedContentPage, AeroError>(AeroError.ValidationError(["Virtual list paging exceeds the provider's bounded 100-entry window."]));
        var filters = scope.Query.Filters ?? [];
        if (!string.IsNullOrWhiteSpace(scope.Query.SortField)
            || filters.Count > 1
            || filters.Any(filter => filter.Operator != PageContentFilterOperator.Contains
                || !string.Equals(filter.FieldName, "$search", StringComparison.Ordinal)))
            return Prelude.Fail<PublishedContentPage, AeroError>(AeroError.ValidationError(["Virtual lists support only one optional Contains search filter on '$search' and no sorting."]));
        var query = filters.Count == 1 ? filters[0].Value : null;
        if (query?.Length > 256)
            return Prelude.Fail<PublishedContentPage, AeroError>(AeroError.ValidationError(["Virtual list search text is too long."]));
        var fetchTake = checked((int)Math.Min(requested + 1, 100));
        var entries = await provider.SearchAsync(viewScope, culture, query, fetchTake, ct);
        var valid = entries.Where(entry => entry.Scope == viewScope && entry.Key.IsValid
                && string.Equals(entry.Key.Provider, provider.Provider, StringComparison.OrdinalIgnoreCase))
            .Take(fetchTake).ToArray();
        var hasMore = requested < 100 && valid.Length > requested;
        var page = valid.Take((int)requested).Skip((int)offset).Take(scope.Query.PageSize)
            .Select(entry => CreateVirtualProjection(entry, provider.Provider, scope.ContentTypeAlias)).ToArray();
        var totalCount = checked(offset + page.Length + (hasMore ? 1 : 0));
        return Prelude.Ok<PublishedContentPage, AeroError>(new PublishedContentPage
        {
            ContentTypeAlias = string.IsNullOrWhiteSpace(scope.ContentTypeAlias) ? provider.Provider : scope.ContentTypeAlias,
            Items = page, TotalCount = totalCount, IsTotalCountExact = !hasMore, HasMore = hasMore,
            PageNumber = pageNumber, PageSize = scope.Query.PageSize
        });
    }

    private static PublishedContentItemProjection CreateVirtualProjection(ContentEntry entry, string provider, string fallbackAlias)
        => new()
        {
            ContentTypeAlias = string.IsNullOrWhiteSpace(fallbackAlias) ? provider : fallbackAlias,
            Slug = entry.Key.StableId,
            Culture = string.Empty,
            Fields = entry.Values.ToDictionary(pair => pair.Key, pair => pair.Value is JsonElement element ? element.Clone() : JsonSerializer.SerializeToElement(pair.Value), StringComparer.OrdinalIgnoreCase)
        };

    private static Result<string, AeroError> ValidateInput(long siteId, string culture)
    {
        if (siteId <= 0)
        {
            return Prelude.Fail<string, AeroError>(
                AeroError.ValidationError(["A site is required to resolve page content."]));
        }

        try
        {
            var normalized = string.IsNullOrWhiteSpace(culture)
                ? "en-US"
                : CultureInfo.GetCultureInfo(culture.Trim()).Name;
            return Prelude.Ok<string, AeroError>(normalized);
        }
        catch (CultureNotFoundException)
        {
            return Prelude.Fail<string, AeroError>(
                AeroError.ValidationError([$"Culture '{culture}' is not valid for page content resolution."]));
        }
    }

    private static AeroError? ValidateQuery(PageContentListScope scope, int pageNumber)
    {
        if (pageNumber <= 0)
        {
            return AeroError.ValidationError([$"Content list scope '{scope.NodeId}' requires a positive page number."]);
        }

        if (scope.Query is null
            || scope.Query.PageSize is < PageContentListQuery.MinimumPageSize
                or > PageContentListQuery.MaximumPageSize
            || (scope.Query.Filters?.Count ?? 0) > PageContentListQuery.MaximumFilterCount)
        {
            return AeroError.ValidationError([$"Content list scope '{scope.NodeId}' has an invalid bounded query."]);
        }

        if (!string.IsNullOrWhiteSpace(scope.ContentEntryProvider)
            && (!string.IsNullOrWhiteSpace(scope.Query.SortField)
                || (scope.Query.Filters?.Count ?? 0) > 1
                || (scope.Query.Filters ?? []).Any(filter => filter.Operator != PageContentFilterOperator.Contains
                    || !string.Equals(filter.FieldName, "$search", StringComparison.Ordinal))))
        {
            return AeroError.ValidationError([$"Virtual content list scope '{scope.NodeId}' supports only one optional Contains search filter on '$search' and no sorting."]);
        }

        return null;
    }

    private static Result<bool, AeroError> ValidatePublishedItem(
        ContentItem item,
        string contentTypeAlias,
        string culture,
        long scopeNodeId)
    {
        if (!string.Equals(item.ContentTypeAlias, contentTypeAlias, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(item.Culture, culture, StringComparison.OrdinalIgnoreCase)
            || item.PublicationState != ContentPublicationState.Published)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.NotFoundError($"Published content for scope '{scopeNodeId}' was not found."));
        }

        return Prelude.Ok<bool, AeroError>(true);
    }

    private static PublishedContentItemProjection CreateProjection(ContentItem item)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in item.Fields ?? [])
        {
            fields[name] = value.Clone();
        }

        return new PublishedContentItemProjection
        {
            Id = item.Id,
            ContentTypeAlias = item.ContentTypeAlias,
            Slug = item.Slug,
            Culture = item.Culture,
            Fields = fields
        };
    }

    private static bool MatchesFilter(ContentItem item, PageContentFilter filter)
    {
        var found = TryGetField(item, filter.FieldName, out var value);
        var empty = !found || IsEmpty(value);
        if (filter.Operator == PageContentFilterOperator.IsEmpty)
        {
            return empty;
        }

        if (filter.Operator == PageContentFilterOperator.IsNotEmpty)
        {
            return !empty;
        }

        if (!found || filter.Value is null)
        {
            return false;
        }

        var comparison = Compare(value, filter.Value);
        var text = ToInvariantText(value);
        return filter.Operator switch
        {
            PageContentFilterOperator.Equals => comparison == 0,
            PageContentFilterOperator.NotEquals => comparison != 0,
            PageContentFilterOperator.Contains => text.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            PageContentFilterOperator.StartsWith => text.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            PageContentFilterOperator.EndsWith => text.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            PageContentFilterOperator.GreaterThan => comparison > 0,
            PageContentFilterOperator.GreaterThanOrEqual => comparison >= 0,
            PageContentFilterOperator.LessThan => comparison < 0,
            PageContentFilterOperator.LessThanOrEqual => comparison <= 0,
            _ => false
        };
    }

    private static void Sort(List<ContentItem> items, PageContentListQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.SortField))
        {
            items.Sort((left, right) =>
            {
                var publication = Nullable.Compare(right.PublishedOn, left.PublishedOn);
                return publication != 0 ? publication : right.Id.CompareTo(left.Id);
            });
            return;
        }

        items.Sort((left, right) =>
        {
            var leftFound = TryGetField(left, query.SortField, out var leftValue);
            var rightFound = TryGetField(right, query.SortField, out var rightValue);
            if (leftFound != rightFound)
            {
                return leftFound ? -1 : 1;
            }

            var comparison = leftFound ? Compare(leftValue, rightValue) : 0;
            if (query.SortDirection == PageContentSortDirection.Descending)
            {
                comparison = -comparison;
            }

            return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
        });
    }

    private static bool TryGetField(ContentItem item, string? fieldName, out JsonElement value)
    {
        if (!string.IsNullOrWhiteSpace(fieldName) && item.Fields is not null)
        {
            if (item.Fields.TryGetValue(fieldName, out value))
            {
                return true;
            }

            foreach (var field in item.Fields)
            {
                if (string.Equals(field.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    value = field.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool IsEmpty(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => true,
        JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
        JsonValueKind.Array => value.GetArrayLength() == 0,
        JsonValueKind.Object => !value.EnumerateObject().Any(),
        _ => false
    };

    private static int Compare(JsonElement left, JsonElement right)
    {
        if (TryGetDecimal(left, out var leftNumber) && TryGetDecimal(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (TryGetDate(left, out var leftDate) && TryGetDate(right, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return StringComparer.OrdinalIgnoreCase.Compare(ToInvariantText(left), ToInvariantText(right));
    }

    private static int Compare(JsonElement left, string right)
    {
        if (TryGetDecimal(left, out var leftNumber)
            && decimal.TryParse(right, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (TryGetDate(left, out var leftDate)
            && DateTimeOffset.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return StringComparer.OrdinalIgnoreCase.Compare(ToInvariantText(left), right);
    }

    private static bool TryGetDecimal(JsonElement value, out decimal number)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out number))
        {
            return true;
        }

        return decimal.TryParse(
            value.ValueKind == JsonValueKind.String ? value.GetString() : null,
            NumberStyles.Number | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool TryGetDate(JsonElement value, out DateTimeOffset date)
        => DateTimeOffset.TryParse(
            value.ValueKind == JsonValueKind.String ? value.GetString() : null,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out date);

    private static string ToInvariantText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => bool.TrueString,
        JsonValueKind.False => bool.FalseString,
        _ => value.GetRawText()
    };
}
