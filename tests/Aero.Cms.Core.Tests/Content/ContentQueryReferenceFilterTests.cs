using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Search;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentQueryReferenceFilterTests
{
    [Test]
    public async Task Search_supports_culture_and_exact_reference_filters()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentSearchDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSearchFacet>(SchemaMode.Flexible)
            .WithSchema<ContentSemanticDocument>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                options.Schema.Analyzers.DefineAnalyzer(ContentSearchConstants.AnalyzerName);
                options.Schema.For<ContentSearchDocument>()
                    .FullTextIndex(
                        document => document.FullText,
                        ContentSearchConstants.AnalyzerName);
            });
        await harness.InitializeAsync();
        var items = new[]
        {
            Species(1, "Wolf", "200", "en-US"),
            Species(2, "Dog", "200", "en-US"),
            Species(3, "Gibbon", "999", "en-US"),
            Species(4, "Loup", "200", "fr-FR")
        };
        harness.Session.Store(items);
        var definition = new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "species",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "genus",
                    FieldType = ContentFieldTypes.Reference,
                    Indexed = true
                }
            ]
        };
        var projection = new ContentSearchProjectionService(
            harness.Session,
            new ContentIndexService([new ReferenceFieldIndexer()]),
            new UnavailableContentEmbeddingGenerator());
        foreach (var item in items)
        {
            await projection.StageUpsertAsync(item, definition, new Dictionary<string, JsonElement>());
        }
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentQueryService(harness.Session);

        var result = await service.SearchAsync(
            1,
            "species",
            new Dictionary<string, string>
            {
                ["__culture"] = "en-US",
                ["__search"] = "wolf",
                ["genus"] = "200"
            },
            CancellationToken.None);

        var ok = result as Result<IReadOnlyList<ContentItem>, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.Select(item => item.Title))
            .IsEquivalentTo(["Wolf"]);
    }

    [Test]
    public async Task Public_full_text_search_is_culture_publication_and_visibility_scoped()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentSearchDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSearchFacet>(SchemaMode.Flexible)
            .WithSchema<ContentSemanticDocument>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                options.Schema.Analyzers.DefineAnalyzer(ContentSearchConstants.AnalyzerName);
                options.Schema.For<ContentSearchDocument>()
                    .FullTextIndex(
                        document => document.FullText,
                        ContentSearchConstants.AnalyzerName);
            });
        await harness.InitializeAsync();
        var visible = Species(10, "Wolf visible", "200", "en-US");
        visible.PublicationState = ContentPublicationState.Published;
        var draft = Species(11, "Wolf draft", "200", "en-US");
        var french = Species(12, "Wolf French", "200", "fr-FR");
        french.PublicationState = ContentPublicationState.Published;
        var hidden = Species(13, "Wolf hidden", "200", "en-US");
        hidden.PublicationState = ContentPublicationState.Published;
        harness.Session.Store(visible, draft, french, hidden);
        var visibleDefinition = new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "species"
        };
        var hiddenDefinition = new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "species",
            IncludeInSearch = false
        };
        var projection = new ContentSearchProjectionService(
            harness.Session,
            new ContentIndexService([]),
            new UnavailableContentEmbeddingGenerator());
        await projection.StageUpsertAsync(visible, visibleDefinition, new Dictionary<string, JsonElement>());
        await projection.StageUpsertAsync(draft, visibleDefinition, new Dictionary<string, JsonElement>());
        await projection.StageUpsertAsync(french, visibleDefinition, new Dictionary<string, JsonElement>());
        await projection.StageUpsertAsync(hidden, hiddenDefinition, new Dictionary<string, JsonElement>());
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentQueryService(harness.Session);

        var result = await service.SearchIndexAsync(new ContentSearchRequest(
            1,
            "species",
            "wolf",
            "en-US",
            ContentSearchMode.FullText,
            PublishedOnly: true,
            Skip: 0,
            Take: 10,
            new Dictionary<string, string>()));

        var success = result as Result<ContentSearchResult>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value.Items.Select(item => item.Title))
            .IsEquivalentTo(["Wolf visible"]);
    }

    private static ContentItem Species(
        long id,
        string title,
        string genusId,
        string culture) =>
        new()
        {
            Id = id,
            SiteId = 1,
            ContentTypeAlias = "species",
            Title = title,
            Slug = title.ToLowerInvariant(),
            Culture = culture,
            Fields = new Dictionary<string, JsonElement>
            {
                ["genus"] = JsonSerializer.SerializeToElement(genusId)
            }
        };
}
