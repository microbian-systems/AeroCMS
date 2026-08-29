using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentSearchProjectionLifecycleTests
{
    [Test]
    public async Task Save_update_and_delete_keep_search_projections_in_the_same_unit_of_work()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSearchDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSearchFacet>(SchemaMode.Flexible)
            .WithSchema<ContentSemanticDocument>(SchemaMode.Flexible)
            .WithSchema<ContentTranslationGroupDocument>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                options.Schema.For<ContentItem>().UseOptimisticConcurrency = true;
                options.Schema.Analyzers.DefineAnalyzer(ContentSearchConstants.AnalyzerName);
                options.Schema.For<ContentSearchDocument>()
                    .FullTextIndex(document => document.FullText, ContentSearchConstants.AnalyzerName);
            });
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 10,
            SiteId = 1,
            Alias = "animal",
            Name = "Animal",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "common-name",
                    FieldType = ContentFieldTypes.Text,
                    Indexed = true,
                    FullTextSearchable = true
                },
                new ContentFieldDefinition
                {
                    Name = "species",
                    FieldType = ContentFieldTypes.Text,
                    LocalizationMode = ContentFieldLocalizationMode.Shared,
                    Indexed = true,
                    FullTextSearchable = true
                }
            ]
        });
        await harness.Session.SaveChangesAsync();

        var projection = new ContentSearchProjectionService(
            harness.Session,
            new ContentIndexService([new TextFieldIndexer()]),
            new UnavailableContentEmbeddingGenerator());
        var service = new AeroContentService(harness.Session, projection);
        var item = new ContentItem
        {
            Id = 0,
            SiteId = 1,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            Title = "Wolf",
            Slug = "wolf",
            Fields = new Dictionary<string, JsonElement>
            {
                ["common-name"] = JsonSerializer.SerializeToElement("Grey wolf"),
                ["species"] = JsonSerializer.SerializeToElement("Canis lupus")
            }
        };

        var created = await service.SaveAsync(item);
        await Assert.That(created.IsSuccess).IsTrue().Because(created.ToString());
        var createdItem = created switch
        {
            Result<ContentItem, AeroError>.Ok success => success.Value,
            _ => throw new InvalidOperationException("The successful save did not return its persisted item.")
        };
        var createdStorageVersion = createdItem.Version;
        await using (var verifyCreate = await harness.Store.QuerySessionAsync())
        {
            var search = await verifyCreate.LoadAsync<ContentSearchDocument>(item.Id);
            var raw = await verifyCreate.LoadAsync<ContentItem>(item.Id);
            var group = await verifyCreate.LoadAsync<ContentTranslationGroupDocument>(item.TranslationGroupId!.Value);
            var facets = await verifyCreate.Query<ContentSearchFacet>()
                .Where(facet => facet.ContentItemId == item.Id)
                .ToListAsync();
            await Assert.That(search).IsNotNull();
            await Assert.That(search!.FullText).Contains("Grey wolf");
            await Assert.That(search.FullText).Contains("Canis lupus");
            await Assert.That(raw!.Fields.ContainsKey("species")).IsFalse();
            await Assert.That(group!.SharedFields["species"].GetString()).IsEqualTo("Canis lupus");
            await Assert.That(facets.Select(facet => facet.NormalizedValue))
                .IsEquivalentTo(["GREY WOLF", "CANIS LUPUS"]);
        }

        var query = new AeroContentQueryService(harness.Session);
        var exact = await query.SearchIndexAsync(new ContentSearchRequest(
            1, "animal", string.Empty, "en-US", ContentSearchMode.FullText,
            PublishedOnly: false, Skip: 0, Take: 10,
            new Dictionary<string, string> { ["species"] = "Canis lupus" }));
        var exactSuccess = exact as Result<ContentSearchResult>.Ok;
        await Assert.That(exactSuccess).IsNotNull();
        await Assert.That(exactSuccess!.Value.Items).HasCount(1);
        await Assert.That(exactSuccess.Value.Items[0].Fields["species"].GetString()).IsEqualTo("Canis lupus");

        var fullText = await query.SearchIndexAsync(new ContentSearchRequest(
            1, "animal", "Canis", "en-US", ContentSearchMode.FullText,
            PublishedOnly: false, Skip: 0, Take: 10,
            new Dictionary<string, string>()));
        var fullTextSuccess = fullText as Result<ContentSearchResult>.Ok;
        await Assert.That(fullTextSuccess).IsNotNull();
        await Assert.That(fullTextSuccess!.Value.Items[0].Fields["species"].GetString()).IsEqualTo("Canis lupus");

        var updated = await service.SaveAsync(new ContentItem
        {
            Id = item.Id,
            Version = createdStorageVersion,
            SiteId = 1,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            Title = "Wolf",
            Slug = "wolf",
            Fields = new Dictionary<string, JsonElement>
            {
                ["common-name"] = JsonSerializer.SerializeToElement("Timber wolf")
            }
        });
        await Assert.That(updated.IsSuccess).IsTrue().Because(updated.ToString());
        await Assert.That(createdItem.Version).IsEqualTo(createdStorageVersion);
        await using (var verifyUpdate = await harness.Store.QuerySessionAsync())
        {
            var facets = await verifyUpdate.Query<ContentSearchFacet>()
                .Where(facet => facet.ContentItemId == item.Id)
                .ToListAsync();
            await Assert.That(facets.Select(facet => facet.NormalizedValue))
                .IsEquivalentTo(["TIMBER WOLF", "CANIS LUPUS"]);
        }

        var staleUpdate = await service.SaveAsync(new ContentItem
        {
            Id = item.Id,
            Version = createdStorageVersion,
            SiteId = 1,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            Title = "Wolf",
            Slug = "wolf",
            Fields = new Dictionary<string, JsonElement>
            {
                ["common-name"] = JsonSerializer.SerializeToElement("Stale wolf")
            }
        });
        await Assert.That(staleUpdate.IsFailure).IsTrue();
        await Assert.That(staleUpdate.ToString()).Contains("Content item changed");

        var deleted = await service.DeleteAsync(1, item.Id);
        await Assert.That(deleted.IsSuccess).IsTrue().Because(deleted.ToString());
        await using var verifyDelete = await harness.Store.QuerySessionAsync();
        await Assert.That(await verifyDelete.LoadAsync<ContentItem>(item.Id)).IsNull();
        await Assert.That(await verifyDelete.LoadAsync<ContentSearchDocument>(item.Id)).IsNull();
        await Assert.That(await verifyDelete.LoadAsync<ContentSemanticDocument>(item.Id)).IsNull();
        await Assert.That(await verifyDelete.Query<ContentSearchFacet>()
                .Where(facet => facet.ContentItemId == item.Id)
                .ToListAsync())
            .IsEmpty();
    }
}
