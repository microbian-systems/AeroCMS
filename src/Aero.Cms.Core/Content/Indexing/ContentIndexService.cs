using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Core.Content.Search;
using Aero.Core;

namespace Aero.Cms.Core.Content.Indexing;

public sealed record ContentSearchArtifacts(
    ContentSearchDocument Document,
    IReadOnlyList<ContentSearchFacet> Facets,
    string SemanticText,
    IReadOnlyList<AeroAiKnowledgeSection> PublicKnowledgeSections,
    IReadOnlyList<AeroAiKnowledgeSection> ManagerKnowledgeSections);

/// <summary>Pure extraction of persisted search projections from an item and its runtime definition.</summary>
public sealed class ContentIndexService(IEnumerable<IContentFieldIndexer> indexers)
{
    public ContentSearchArtifacts BuildIndex(
        ContentItem item,
        ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(definition);

        var lookup = indexers.ToDictionary(
            indexer => indexer.FieldType,
            StringComparer.OrdinalIgnoreCase);
        var fullText = new List<string> { item.Title ?? string.Empty, item.Slug };
        var semanticText = new List<string>();
        var facets = new List<ContentSearchFacet>();
        var metadataSection = new AeroAiKnowledgeSection(
            "Entry",
            string.Join(
                ' ',
                new[] { item.Title, item.Slug }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
            AeroAiFieldExposure.Public);
        var publicKnowledge = new List<AeroAiKnowledgeSection> { metadataSection };
        var managerKnowledge = new List<AeroAiKnowledgeSection> { metadataSection };

        foreach (var field in definition.Fields)
        {
            if (!item.Fields.TryGetValue(field.Name, out var value)
                || !lookup.TryGetValue(field.FieldType, out var indexer))
            {
                continue;
            }

            var tokens = indexer.GetIndexTokens(field, value)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Select(token => token.Trim())
                .ToArray();

            if (field.Indexed || field.FieldType == ContentFieldTypes.Reference)
            {
                for (var ordinal = 0; ordinal < tokens.Length; ordinal++)
                {
                    facets.Add(new ContentSearchFacet
                    {
                        Id = Snowflake.NewId(),
                        SiteId = item.SiteId,
                        ContentItemId = item.Id,
                        ContentTypeAlias = item.ContentTypeAlias,
                        Culture = item.Culture,
                        PublicationState = item.PublicationState,
                        HideFromSearch = !definition.IncludeInSearch,
                        FieldName = field.Name,
                        NormalizedValue = NormalizeExactValue(tokens[ordinal]),
                        Ordinal = ordinal
                    });
                }
            }

            if (field.FullTextSearchable)
            {
                fullText.AddRange(tokens);
            }

            if (field.SemanticSearchable)
            {
                semanticText.AddRange(tokens);
            }

            if ((field.FullTextSearchable || field.SemanticSearchable)
                && tokens.Length > 0)
            {
                var section = new AeroAiKnowledgeSection(
                    string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label,
                    string.Join(' ', tokens),
                    field.AiExposure);
                if (AeroAiContentExposureRules.IsFieldAvailable(
                        AeroAiAudience.Public,
                        field.AiExposure))
                {
                    publicKnowledge.Add(section);
                }
                if (AeroAiContentExposureRules.IsFieldAvailable(
                        AeroAiAudience.Manager,
                        field.AiExposure))
                {
                    managerKnowledge.Add(section);
                }
            }
        }

        return new ContentSearchArtifacts(
            new ContentSearchDocument
            {
                Id = item.Id,
                SiteId = item.SiteId,
                ContentItemId = item.Id,
                ContentTypeAlias = item.ContentTypeAlias,
                Culture = item.Culture,
                PublicationState = item.PublicationState,
                PublishedOn = item.PublishedOn,
                VersionNumber = item.VersionNumber,
                Slug = item.Slug,
                Title = item.Title ?? string.Empty,
                HideFromSearch = !definition.IncludeInSearch,
                FullText = string.Join(
                    ' ',
                    fullText.Where(part => !string.IsNullOrWhiteSpace(part)))
            },
            facets,
            string.Join(
                ' ',
                semanticText.Where(part => !string.IsNullOrWhiteSpace(part))),
            publicKnowledge,
            managerKnowledge);
    }

    public static string NormalizeExactValue(string value)
        => value.Trim().ToUpperInvariant();
}
