using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentTypeServiceScopeTests
{
    [Test]
    public async Task Nonzero_missing_or_foreign_id_fails_while_same_alias_is_allowed_across_sites()
    {
        await using var harness = new SableTestHarness().WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument { Id = 1, SiteId = 2, Alias = "article", Name = "Foreign" });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(harness.Session, [], new ScribanTemplateValidator());

        var missing = await service.SaveAsync(new ContentTypeDefinition { Id = 99, SiteId = 1, Alias = "article", Name = "Missing" });
        var foreign = await service.SaveAsync(new ContentTypeDefinition { Id = 1, SiteId = 1, Alias = "article", Name = "Attacker" });
        var created = await service.SaveAsync(new ContentTypeDefinition
        {
            Id = 0, SiteId = 1, Alias = "article", Name = "Local",
            Fields = [new ContentFieldDefinition { Name = "title", FieldType = "text" }],
            ScribanTemplate = "<h1>{{ fields.title }}</h1>"
        });

        await Assert.That(missing.IsFailure).IsTrue();
        await Assert.That(foreign.IsFailure).IsTrue();
        await Assert.That(created.IsSuccess).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<ContentTypeDocument>(1))!.SiteId).IsEqualTo(2);
    }

    [Test]
    public async Task Existing_content_type_alias_requires_an_explicit_conversion_workflow()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 10,
            SiteId = 1,
            Alias = "article",
            Name = "Article"
        });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            Id = 10,
            SiteId = 1,
            Alias = "renamed-article",
            Name = "Article"
        });

        await Assert.That(result.IsFailure).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That((await verify.LoadAsync<ContentTypeDocument>(10))!.Alias)
            .IsEqualTo("article");
    }

    [Test]
    public async Task Hierarchical_content_type_requires_collection_cardinality()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "category",
            Name = "Category",
            Cardinality = ContentCardinality.Singleton,
            Structure = ContentStructure.Hierarchical,
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "title",
                    FieldType = "text"
                }
            ]
        });

        var failure = result as Result<ContentTypeDefinition, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Error).IsTypeOf<AeroError.Validation>();
        var validation = (AeroError.Validation)failure.Error;
        await Assert.That(validation.Errors).Contains(
            "Hierarchical content types must use collection cardinality because a hierarchy contains multiple entries.");

        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That(await verify.Query<ContentTypeDocument>().ToListAsync())
            .IsEmpty();
    }

    [Test]
    public async Task Existing_hierarchical_singleton_can_be_corrected_to_collection()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 20,
            SiteId = 1,
            Alias = "category",
            Name = "Category",
            Cardinality = ContentCardinality.Singleton,
            Structure = ContentStructure.Hierarchical
        });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            Id = 20,
            SiteId = 1,
            Alias = "category",
            Name = "Category",
            Cardinality = ContentCardinality.Collection,
            Structure = ContentStructure.Hierarchical,
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "title",
                    FieldType = "text"
                }
            ]
        });

        await Assert.That(result.IsSuccess).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        var corrected = await verify.LoadAsync<ContentTypeDocument>(20);
        await Assert.That(corrected).IsNotNull();
        await Assert.That(corrected!.Cardinality)
            .IsEqualTo(ContentCardinality.Collection);
        await Assert.That(corrected.Structure)
            .IsEqualTo(ContentStructure.Hierarchical);
    }

    [Test]
    public async Task Hierarchy_reference_requires_a_hierarchical_target_in_the_same_site()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument
            {
                Id = 30,
                SiteId = 1,
                Alias = "flat-category",
                Name = "Flat category",
                Structure = ContentStructure.Flat
            },
            new ContentTypeDocument
            {
                Id = 31,
                SiteId = 1,
                Alias = "taxonomy",
                Name = "Taxonomy",
                Cardinality = ContentCardinality.Collection,
                Structure = ContentStructure.Hierarchical
            },
            new ContentTypeDocument
            {
                Id = 32,
                SiteId = 2,
                Alias = "foreign-taxonomy",
                Name = "Foreign taxonomy",
                Cardinality = ContentCardinality.Collection,
                Structure = ContentStructure.Hierarchical
            });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());

        var missing = await service.SaveAsync(
            ReferencingType("species-missing", "missing-taxonomy"));
        var flat = await service.SaveAsync(
            ReferencingType("species-flat", "flat-category"));
        var foreign = await service.SaveAsync(
            ReferencingType("species-foreign", "foreign-taxonomy"));
        var valid = await service.SaveAsync(
            ReferencingType("species", "taxonomy"));

        await Assert.That(missing.IsFailure).IsTrue();
        await Assert.That(flat.IsFailure).IsTrue();
        await Assert.That(foreign.IsFailure).IsTrue();
        await Assert.That(valid.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Cascading_reference_requires_compatible_relationships_and_is_always_indexed()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument
            {
                Id = 40,
                SiteId = 1,
                Alias = "kingdom",
                Name = "Kingdom"
            },
            new ContentTypeDocument
            {
                Id = 41,
                SiteId = 1,
                Alias = "phylum",
                Name = "Phylum",
                Fields =
                [
                    FlatReference("kingdom", "kingdom")
                ]
            });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());
        var kingdom = FlatReference("kingdom", "kingdom");
        var phylum = FlatReference("phylum", "phylum");
        phylum.Settings[ReferenceContentFieldSettings.DependsOnField] =
            JsonSerializer.SerializeToElement("kingdom");
        phylum.Settings[ReferenceContentFieldSettings.TargetFilterField] =
            JsonSerializer.SerializeToElement("kingdom");

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "animal",
            Name = "Animal",
            Fields =
            [
                kingdom,
                phylum,
                new ContentFieldDefinition
                {
                    Name = "description",
                    FieldType = ContentFieldTypes.RichText,
                    FullTextSearchable = true,
                    SemanticSearchable = true
                }
            ]
        });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(kingdom.Indexed).IsTrue();
        await Assert.That(phylum.Indexed).IsTrue();
    }

    [Test]
    public async Task Semantic_search_rejects_non_text_fields()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());

        var result = await service.SaveAsync(new ContentTypeDefinition
        {
            SiteId = 1,
            Alias = "measurement",
            Name = "Measurement",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "value",
                    FieldType = ContentFieldTypes.Number,
                    SemanticSearchable = true
                }
            ]
        });

        await Assert.That(result.IsFailure).IsTrue();
    }

    private static ContentTypeDefinition ReferencingType(
        string alias,
        string targetAlias) =>
        new()
        {
            SiteId = 1,
            Alias = alias,
            Name = alias,
            Cardinality = ContentCardinality.Collection,
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "classification",
                    Label = "Classification",
                    FieldType = ContentFieldTypes.Reference,
                    Settings = new Dictionary<string, JsonElement>
                    {
                        [ReferenceContentFieldSettings.TargetContentType] =
                            JsonSerializer.SerializeToElement(targetAlias),
                        [ReferenceContentFieldSettings.SelectionMode] =
                            JsonSerializer.SerializeToElement(
                                ReferenceContentFieldSettings.SelectionModeHierarchy)
                    }
                }
            ]
        };

    private static ContentFieldDefinition FlatReference(
        string name,
        string targetAlias) =>
        new()
        {
            Name = name,
            Label = name,
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetContentType] =
                    JsonSerializer.SerializeToElement(targetAlias)
            }
        };
}
