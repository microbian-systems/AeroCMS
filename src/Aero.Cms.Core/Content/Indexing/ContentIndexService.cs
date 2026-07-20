using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>
/// Builds a search index document from a ContentItem by extracting field-level tokens
/// using registered IContentFieldIndexer implementations.
/// </summary>
/// <remarks>
/// Indexers are matched to field types case-insensitively. Duplicate registrations for
/// the same field type cause index construction to fail.
/// </remarks>
public sealed class ContentIndexService(
    IEnumerable<IContentFieldIndexer> indexers,
    IContentTypeService typeService)
{
    /// <summary>
    /// Builds a <see cref="ContentSearchDocument"/> for the given content item.
    /// Returns an identifier-only document if the content type cannot be resolved.
    /// </summary>
    /// <param name="item">The content item to index.</param>
    /// <param name="ct">A token that can cancel content-type resolution.</param>
    /// <returns>
    /// A search document containing tokens from fields that have both a value and a
    /// registered indexer. Hidden content types return metadata without field tokens.
    /// </returns>
    /// <remarks>
    /// Tokens are preserved as returned by each indexer. They are joined with spaces into
    /// <see cref="ContentSearchDocument.FullText"/>; no additional case or culture
    /// normalization is applied.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Multiple indexers have the same field type, ignoring case.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    public async Task<ContentSearchDocument> BuildIndexAsync(ContentItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var typeResult = await typeService.GetByAliasAsync(item.SiteId, item.ContentTypeAlias, ct);
        if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return new ContentSearchDocument { Id = $"content:{item.SiteId}:{item.Id}" };

        var type = typeOk.Value;
        var doc = new ContentSearchDocument
        {
            Id = $"content:{item.SiteId}:{item.Id}",
            SiteId = item.SiteId,
            ContentItemId = item.Id,
            ContentTypeAlias = item.ContentTypeAlias,
            Slug = item.Slug,
            Title = item.Title ?? "",
            HideFromSearch = type.HideFromSearch
        };

        if (type.HideFromSearch)
            return doc;

        var lookup = indexers.ToDictionary(x => x.FieldType, StringComparer.OrdinalIgnoreCase);

        foreach (var field in type.Fields)
        {
            if (!item.Fields.TryGetValue(field.Name, out var element)) continue;
            if (!lookup.TryGetValue(field.FieldType, out var indexer)) continue;

            var tokens = indexer.GetIndexTokens(field, element).ToList();
            doc.FieldTokens[field.Name] = tokens;
            doc.FullText += string.Join(" ", tokens) + " ";
        }

        return doc;
    }
}
