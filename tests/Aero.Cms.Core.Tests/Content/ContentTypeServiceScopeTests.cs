using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
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
    public async Task Missing_alias_is_classified_as_not_found()
    {
        await using var harness = new SableTestHarness().WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var service = new AeroContentTypeService(harness.Session, [], new ScribanTemplateValidator());

        var result = await service.GetByAliasAsync(1, "missing");

        await Assert.That(result is Result<ContentTypeDefinition, AeroError>.Failure { Error: AeroError.NotFound }).IsTrue();
    }

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
            ReferencingType("species-missing", 99));
        var flat = await service.SaveAsync(
            ReferencingType("species-flat", 30));
        var foreign = await service.SaveAsync(
            ReferencingType("species-foreign", 32));
        var valid = await service.SaveAsync(
            ReferencingType("species", 31));
        var aliasShaped = ReferencingType("species-alias", 31);
        aliasShaped.Fields[0].Settings[ReferenceContentFieldSettings.TargetContentTypeId] =
            JsonSerializer.SerializeToElement("taxonomy");
        var aliasResult = await service.SaveAsync(aliasShaped);

        await Assert.That(missing.IsFailure).IsTrue();
        await Assert.That(flat.IsFailure).IsTrue();
        await Assert.That(foreign.IsFailure).IsTrue();
        await Assert.That(valid.IsSuccess).IsTrue();
        await Assert.That(aliasResult.IsFailure).IsTrue();
    }

    [Test]
    public async Task Delete_rejects_external_reference_preserves_target_and_allows_self_reference()
    {
        await using var harness = new SableTestHarness().WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var target = new ContentTypeDocument { Id = 70, SiteId = 1, Alias = "target", Name = "Target" };
        harness.Session.Store(target, new ContentTypeDocument
        {
            Id = 71, SiteId = 1, Alias = "dependent", Name = "Dependent",
            Fields = [FlatReference("target", 70)]
        });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(harness.Session, [], new ScribanTemplateValidator());

        var blocked = await service.DeleteAsync(1, "target");
        await Assert.That(blocked.IsFailure).IsTrue();
        var preserved = await harness.Session.LoadAsync<ContentTypeDocument>(70);
        await Assert.That(preserved).IsNotNull();

        harness.Session.Delete(await harness.Session.LoadAsync<ContentTypeDocument>(71));
        target.Fields = [FlatReference("self", 70)];
        harness.Session.Store(target);
        await harness.Session.SaveChangesAsync();
        var deleted = await service.DeleteAsync(1, "target");
        await Assert.That(deleted.IsSuccess).IsTrue();
        await Assert.That(((Result<bool, AeroError>.Ok)deleted).Value).IsTrue();
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
                    FlatReference("kingdom", 40)
                ]
            });
        await harness.Session.SaveChangesAsync();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator());
        var kingdom = FlatReference("kingdom", 40);
        var phylum = FlatReference("phylum", 41);
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

    [Test]
    public async Task Native_relationship_requires_one_materializer_and_a_shared_reference()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 90,
            SiteId = 1,
            Alias = "species",
            Name = "Species"
        });
        await harness.Session.SaveChangesAsync();
        var definition = NativeRelationshipType("animal_species", ContentFieldLocalizationMode.Shared);

        var missing = await new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator(),
            []).SaveAsync(definition);
        await Assert.That(missing.IsFailure).IsTrue();

        var materializer = new ContentTypeReferenceRelationshipMaterializer();
        var ambiguous = await new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator(),
            [materializer, materializer]).SaveAsync(definition);
        await Assert.That(ambiguous.IsFailure).IsTrue();

        var localized = await new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator(),
            [materializer]).SaveAsync(
                NativeRelationshipType("animal_species", ContentFieldLocalizationMode.Localized));
        await Assert.That(localized.IsFailure).IsTrue();

        var saved = await new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator(),
            [materializer]).SaveAsync(definition);
        await Assert.That(saved.IsSuccess).IsTrue();

        var competing = NativeRelationshipType("animal_species", ContentFieldLocalizationMode.Shared);
        competing.Alias = "habitat";
        competing.Name = "Habitat";
        var duplicateAcrossTypes = await new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator(),
            [materializer]).SaveAsync(competing);
        await Assert.That(duplicateAcrossTypes.IsFailure).IsTrue();
    }

    [Test]
    public async Task Existing_native_relationship_change_requires_an_explicit_backfill_when_entries_exist()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible)
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTranslationGroupDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument
            {
                Id = 90,
                SiteId = 1,
                Alias = "species",
                Name = "Species"
            },
            new ContentTypeDocument
            {
                Id = 91,
                SiteId = 1,
                Alias = "animal",
                Name = "Animal",
                Fields = [NativeRelationshipField("animal_species", ContentFieldLocalizationMode.Shared)]
            });
        harness.Session.Store(new ContentItem
        {
            Id = 92,
            SiteId = 1,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            Slug = "animal",
            Title = "Animal"
        });
        await harness.Session.SaveChangesAsync();
        var materializer = new ContentTypeReferenceRelationshipMaterializer();
        var service = new AeroContentTypeService(
            harness.Session,
            [],
            new ScribanTemplateValidator(),
            [materializer]);

        var changed = NativeRelationshipType("animal_species_v2", ContentFieldLocalizationMode.Shared);
        changed.Id = 91;
        var result = await service.SaveAsync(changed);

        await Assert.That(result.IsFailure).IsTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        var stored = await verify.LoadAsync<ContentTypeDocument>(91);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.Fields[0].Settings[ReferenceContentFieldSettings.RelationshipAlias].GetString())
            .IsEqualTo("animal_species");
    }

    private static ContentTypeDefinition NativeRelationshipType(
        string relationshipAlias,
        ContentFieldLocalizationMode localizationMode) =>
        new()
        {
            SiteId = 1,
            Alias = "animal",
            Name = "Animal",
            Fields = [NativeRelationshipField(relationshipAlias, localizationMode)]
        };

    private static ContentFieldDefinition NativeRelationshipField(
        string relationshipAlias,
        ContentFieldLocalizationMode localizationMode) =>
        new()
        {
            Name = "species",
            Label = "Species",
            FieldType = ContentFieldTypes.Reference,
            LocalizationMode = localizationMode,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetContentTypeId] = JsonSerializer.SerializeToElement("90"),
                [ReferenceContentFieldSettings.RelationshipAlias] = JsonSerializer.SerializeToElement(relationshipAlias)
            }
        };

    private static ContentTypeDefinition ReferencingType(
        string alias,
        long targetId) =>
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
                        [ReferenceContentFieldSettings.TargetContentTypeId] =
                            JsonSerializer.SerializeToElement(targetId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        [ReferenceContentFieldSettings.SelectionMode] =
                            JsonSerializer.SerializeToElement(
                                ReferenceContentFieldSettings.SelectionModeHierarchy)
                    }
                }
            ]
        };

    private static ContentFieldDefinition FlatReference(
        string name,
        long targetId) =>
        new()
        {
            Name = name,
            Label = name,
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetContentTypeId] =
                    JsonSerializer.SerializeToElement(targetId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            }
        };
}
