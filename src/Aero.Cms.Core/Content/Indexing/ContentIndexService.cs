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
public sealed class ContentIndexService(
    IEnumerable<IContentFieldIndexer> indexers,
    IContentTypeService typeService)
{
    /// <summary>
    /// Builds a <see cref="ContentSearchDocument"/> for the given content item.
    /// Returns a document with at minimum the Id populated if the content type cannot be resolved.
    /// </summary>
    /// <param name="item">The content item to index.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A search document containing indexed field tokens.</returns>
    public async Task<ContentSearchDocument> BuildIndexAsync(ContentItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var typeResult = await typeService.GetByAliasAsync(item.SiteId, item.ContentTypeAlias, ct);
        if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk)
            return new ContentSearchDocument { Id = $"content:{item.SiteId}:{item.Id}" };

        var type = typeOk.Value;
        var lookup = indexers.ToDictionary(x => x.FieldType, StringComparer.OrdinalIgnoreCase);

        var doc = new ContentSearchDocument
        {
            Id = $"content:{item.SiteId}:{item.Id}",
            SiteId = item.SiteId,
            ContentItemId = item.Id,
            ContentTypeAlias = item.ContentTypeAlias,
            Slug = item.Slug,
            Title = item.Title ?? ""
        };

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
