using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Core.Content.Indexing;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentIndexServiceTests
{
    [Test]
    public async Task Search_capabilities_shape_persistable_text_facets_and_semantic_source()
    {
        var definition = new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "animal",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "name",
                    FieldType = ContentFieldTypes.Text,
                    Indexed = true,
                    FullTextSearchable = true
                },
                new ContentFieldDefinition
                {
                    Name = "description",
                    FieldType = ContentFieldTypes.RichText,
                    SemanticSearchable = true
                },
                new ContentFieldDefinition
                {
                    Name = "genus",
                    FieldType = ContentFieldTypes.Reference
                },
                new ContentFieldDefinition
                {
                    Name = "related",
                    FieldType = ContentFieldTypes.Reference
                },
                new ContentFieldDefinition
                {
                    Name = "featured-page",
                    FieldType = ContentFieldTypes.Reference,
                    Settings = new Dictionary<string, JsonElement>
                    {
                        [ReferenceContentFieldSettings.TargetKind] =
                            JsonSerializer.SerializeToElement(
                                ReferenceContentFieldSettings.TargetKindCmsDocument)
                    }
                },
                new ContentFieldDefinition
                {
                    Name = "website",
                    FieldType = ContentFieldTypes.Url,
                    FullTextSearchable = true
                },
                new ContentFieldDefinition
                {
                    Name = "weight",
                    FieldType = ContentFieldTypes.Number,
                    Indexed = true
                },
                new ContentFieldDefinition
                {
                    Name = "private-note",
                    FieldType = ContentFieldTypes.Text
                }
            ]
        };
        var service = new ContentIndexService(
        [
            new TextFieldIndexer(),
            new UrlFieldIndexer(),
            new RichTextFieldIndexer(),
            new ReferenceFieldIndexer(),
            new NumberFieldIndexer()
        ]);
        var item = new ContentItem
        {
            Id = 42,
            SiteId = 1,
            ContentTypeAlias = "animal",
            Title = "Wolf",
            Slug = "wolf",
            Culture = "en-US",
            Fields = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Grey wolf"),
                ["description"] = JsonSerializer.SerializeToElement("<p>A social canid.</p>"),
                ["genus"] = JsonSerializer.SerializeToElement("200"),
                ["related"] = JsonSerializer.SerializeToElement(new[] { "200", "300" }),
                ["featured-page"] = JsonSerializer.SerializeToElement(
                    new CmsContentReferenceValue(
                        CmsContentReferenceSources.Pages,
                        "1530221140281556994"),
                    ContentJsonContext.Default.CmsContentReferenceValue),
                ["website"] = JsonSerializer.SerializeToElement("https://example.test/wolf"),
                ["weight"] = JsonSerializer.SerializeToElement(12.5m),
                ["private-note"] = JsonSerializer.SerializeToElement("hidden")
            }
        };

        var artifacts = service.BuildIndex(item, definition);

        await Assert.That(artifacts.Document.FullText.Contains("Grey wolf")).IsTrue();
        await Assert.That(artifacts.Document.FullText.Contains("hidden")).IsFalse();
        await Assert.That(artifacts.Document.FullText.Contains("https://example.test/wolf")).IsTrue();
        await Assert.That(artifacts.SemanticText.Contains("A social canid.")).IsTrue();
        await Assert.That(artifacts.SemanticText.Contains("Grey wolf")).IsFalse();
        await Assert.That(artifacts.Facets.Any(
            facet => facet.FieldName == "name" && facet.NormalizedValue == "GREY WOLF")).IsTrue();
        await Assert.That(artifacts.Facets.Any(
            facet => facet.FieldName == "genus" && facet.NormalizedValue == "200")).IsTrue();
        await Assert.That(artifacts.Facets
                .Where(facet => facet.FieldName == "related")
                .Select(facet => facet.NormalizedValue))
            .IsEquivalentTo(["200", "300"]);
        await Assert.That(artifacts.Facets.Any(
            facet => facet.FieldName == "featured-page"
                && facet.NormalizedValue == "PAGES:1530221140281556994")).IsTrue();
        await Assert.That(artifacts.Facets.Any(
            facet => facet.FieldName == "weight" && facet.NormalizedValue == "12.5")).IsTrue();
    }
}
