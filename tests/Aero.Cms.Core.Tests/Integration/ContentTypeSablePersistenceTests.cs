using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class ContentTypeSablePersistenceTests
{
    [Test]
    public async Task Strict_content_documents_round_trip_complex_field_bags_and_updates()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Strict)
            .WithSchema<ContentTypeDocument>(SchemaMode.Strict);
        await harness.InitializeAsync();

        var type = new ContentTypeDocument
        {
            Id = 91_001,
            SiteId = 42,
            Alias = "article",
            Name = "Article",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "body",
                    FieldType = "richtext",
                    Required = true,
                    Settings = new Dictionary<string, JsonElement>
                    {
                        ["maxLength"] = Json("5000"),
                        ["toolbar"] = Json("""["bold","italic","link"]"""),
                        ["editor"] = Json("""{"spellcheck":true,"height":320}""")
                    }
                }
            ]
        };

        var item = new ContentItem
        {
            Id = 91_002,
            SiteId = 42,
            ContentTypeAlias = type.Alias,
            Culture = "en-US",
            Slug = "nested-content",
            Title = "Nested content",
            Fields = new Dictionary<string, JsonElement>
            {
                ["text"] = Json("\"Hello\""),
                ["number"] = Json("42.5"),
                ["enabled"] = Json("true"),
                ["publishedOn"] = Json("\"2026-07-17T12:30:00Z\""),
                ["metadata"] = Json("""{"author":{"name":"Ada"},"tags":["cms","surrealdb"]}"""),
                ["sections"] = Json("""[{"kind":"hero","columns":2},{"kind":"copy","columns":1}]""")
            }
        };

        harness.Session.Store(type);
        harness.Session.Store(item);
        await harness.Session.SaveChangesAsync();

        await using (var verificationSession = await harness.OpenSessionAsync(
                         new SessionOptions { Tracking = DocumentTracking.None }))
        {
            var savedType = (await verificationSession.Query<ContentTypeDocument>().ToListAsync())
                .Single(document => document.Alias == "article");
            var savedItem = (await verificationSession.Query<ContentItem>().ToListAsync())
                .Single(document => document.Slug == "nested-content");

            savedType.Fields.Single().Settings["maxLength"].GetInt32().ShouldBe(5000);
            savedType.Fields.Single().Settings["toolbar"].GetArrayLength().ShouldBe(3);
            savedType.Fields.Single().Settings["editor"].GetProperty("spellcheck").GetBoolean().ShouldBeTrue();

            savedItem.Fields["text"].GetString().ShouldBe("Hello");
            savedItem.Fields["number"].GetDecimal().ShouldBe(42.5m);
            savedItem.Fields["enabled"].GetBoolean().ShouldBeTrue();
            savedItem.Fields["metadata"].GetProperty("author").GetProperty("name").GetString().ShouldBe("Ada");
            savedItem.Fields["sections"].GetArrayLength().ShouldBe(2);
        }

        await using (var updateSession = await harness.OpenSessionAsync())
        {
            var savedItem = (await updateSession.Query<ContentItem>().ToListAsync())
                .Single(document => document.Slug == "nested-content");
            savedItem.Fields["text"] = Json("\"Updated\"");
            savedItem.Fields["metadata"] = Json("""{"author":{"name":"Grace"},"tags":["updated"]}""");
            updateSession.Store(savedItem);
            await updateSession.SaveChangesAsync();
        }

        await using var updatedVerificationSession = await harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var updated = (await updatedVerificationSession.Query<ContentItem>().ToListAsync())
            .Single(document => document.Slug == "nested-content");

        updated.Fields["text"].GetString().ShouldBe("Updated");
        updated.Fields["metadata"].GetProperty("author").GetProperty("name").GetString().ShouldBe("Grace");
        updated.Fields["metadata"].GetProperty("tags").GetArrayLength().ShouldBe(1);
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
