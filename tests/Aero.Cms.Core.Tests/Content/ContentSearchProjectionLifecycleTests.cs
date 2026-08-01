using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Services;
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
            .WithSchema<ContentSemanticDocument>(SchemaMode.Flexible);
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
                ["common-name"] = JsonSerializer.SerializeToElement("Grey wolf")
            }
        };

        var created = await service.SaveAsync(item);
        await Assert.That(created.IsSuccess).IsTrue().Because(created.ToString());
        await using (var verifyCreate = await harness.Store.QuerySessionAsync())
        {
            var search = await verifyCreate.LoadAsync<ContentSearchDocument>(item.Id);
            var facets = await verifyCreate.Query<ContentSearchFacet>()
                .Where(facet => facet.ContentItemId == item.Id)
                .ToListAsync();
            await Assert.That(search).IsNotNull();
            await Assert.That(search!.FullText).Contains("Grey wolf");
            await Assert.That(facets.Select(facet => facet.NormalizedValue))
                .IsEquivalentTo(["GREY WOLF"]);
        }

        var updated = await service.SaveAsync(new ContentItem
        {
            Id = item.Id,
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
        await using (var verifyUpdate = await harness.Store.QuerySessionAsync())
        {
            var facets = await verifyUpdate.Query<ContentSearchFacet>()
                .Where(facet => facet.ContentItemId == item.Id)
                .ToListAsync();
            await Assert.That(facets.Select(facet => facet.NormalizedValue))
                .IsEquivalentTo(["TIMBER WOLF"]);
        }

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
