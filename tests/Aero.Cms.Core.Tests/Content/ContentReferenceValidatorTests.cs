using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentReferenceValidatorTests
{
    [Test]
    public async Task Hierarchy_reference_enforces_target_type_and_optional_leaf_selection()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new ContentItem
        {
            Id = 11,
            SiteId = 1,
            ContentTypeAlias = "taxonomy",
            ParentId = 10,
            Title = "Child"
        });
        await harness.Session.SaveChangesAsync();

        var contentService = Substitute.For<IContentService>();
        contentService.LoadAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 10,
                SiteId = 1,
                ContentTypeAlias = "taxonomy",
                Title = "Parent"
            }));
        contentService.LoadAsync(1, 12, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 12,
                SiteId = 1,
                ContentTypeAlias = "taxonomy",
                Title = "Leaf"
            }));
        contentService.LoadAsync(1, 13, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 13,
                SiteId = 1,
                ContentTypeAlias = "other",
                Title = "Wrong type"
            }));
        var validator = new ReferenceExistenceValidator(
            contentService,
            harness.Session);
        var type = Definition();

        var parentFailures = await validator.ValidateAsync(
            ItemWithReference(10),
            type,
            CancellationToken.None);
        var leafFailures = await validator.ValidateAsync(
            ItemWithReference(12),
            type,
            CancellationToken.None);
        var wrongTypeFailures = await validator.ValidateAsync(
            ItemWithReference(13),
            type,
            CancellationToken.None);

        await Assert.That(parentFailures.Select(failure => failure.ErrorMessage))
            .Contains("Referenced item '10' must be a leaf entry without children.");
        await Assert.That(leafFailures).IsEmpty();
        await Assert.That(wrongTypeFailures.Select(failure => failure.ErrorMessage))
            .Contains("Referenced item '13' is not a 'taxonomy' entry.");
    }

    [Test]
    public async Task Cascading_reference_requires_the_target_to_link_to_the_selected_parent()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var contentService = Substitute.For<IContentService>();
        contentService.LoadAsync(1, 300, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 300,
                SiteId = 1,
                ContentTypeAlias = "species",
                Title = "Canis lupus",
                Fields = new Dictionary<string, JsonElement>
                {
                    ["genus"] = JsonSerializer.SerializeToElement("200")
                }
            }));
        contentService.LoadAsync(1, 301, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 301,
                SiteId = 1,
                ContentTypeAlias = "species",
                Title = "Unrelated species",
                Fields = new Dictionary<string, JsonElement>
                {
                    ["genus"] = JsonSerializer.SerializeToElement("999")
                }
            }));
        contentService.LoadAsync(1, 200, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 200,
                SiteId = 1,
                ContentTypeAlias = "genus",
                Title = "Canis"
            }));
        var validator = new ReferenceExistenceValidator(
            contentService,
            harness.Session);
        var type = new ContentTypeDefinition
        {
            Alias = "animal",
            Fields =
            [
                ReferenceField("genus", "genus"),
                ReferenceField(
                    "species",
                    "species",
                    dependsOn: "genus",
                    targetFilter: "genus")
            ]
        };

        var valid = await validator.ValidateAsync(
            CascadingItem(300),
            type,
            CancellationToken.None);
        var invalid = await validator.ValidateAsync(
            CascadingItem(301),
            type,
            CancellationToken.None);

        await Assert.That(valid).IsEmpty();
        await Assert.That(invalid.Select(failure => failure.ErrorMessage))
            .Contains(
                "Referenced item '301' does not belong to the selected 'genus' entry.");
    }

    [Test]
    public async Task Cms_document_reference_uses_the_registered_site_scoped_provider()
    {
        var contentService = Substitute.For<IContentService>();
        var session = Substitute.For<IDocumentSession>();
        var provider = Substitute.For<IContentReferenceSourceProvider>();
        provider.SourceKey.Returns(CmsContentReferenceSources.Pages);
        provider.DisplayName.Returns("Pages");
        provider.ExistsAsync(42, 1530221140281556994, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<bool>>(
                new Result<bool>.Ok(true)));
        var validator = new ReferenceExistenceValidator(
            contentService,
            session,
            [provider]);
        var type = new ContentTypeDefinition
        {
            Alias = "feature",
            Fields = [CmsDocumentReferenceField()]
        };
        var item = new ContentItem
        {
            SiteId = 42,
            ContentTypeAlias = "feature",
            Fields = new Dictionary<string, JsonElement>
            {
                ["related-page"] = JsonSerializer.SerializeToElement(
                    new CmsContentReferenceValue(
                        CmsContentReferenceSources.Pages,
                        "1530221140281556994"),
                    ContentJsonContext.Default.CmsContentReferenceValue)
            }
        };

        var failures = await validator.ValidateAsync(
            item,
            type,
            CancellationToken.None);

        await Assert.That(failures).IsEmpty();
        await provider.Received(1).ExistsAsync(
            42,
            1530221140281556994,
            Arg.Any<CancellationToken>());
        await contentService.DidNotReceiveWithAnyArgs()
            .LoadAsync(default, default, default);
    }

    [Test]
    public async Task Cms_document_reference_accepts_a_public_content_entry_of_the_selected_type()
    {
        var contentService = Substitute.For<IContentService>();
        contentService.LoadAsync(42, 300, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(new ContentItem
            {
                Id = 300,
                SiteId = 42,
                ContentTypeAlias = "species",
                Title = "K-9"
            }));
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.GetByAliasAsync(
                42,
                "species",
                Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentTypeDefinition, AeroError>(
                new ContentTypeDefinition
                {
                    SiteId = 42,
                    Alias = "species",
                    Name = "Species",
                    AllowPublicUrl = true
                }));
        var validator = new ReferenceExistenceValidator(
            contentService,
            Substitute.For<IDocumentSession>(),
            contentTypeService: contentTypeService);
        var item = CmsContentEntryReferenceItem(
            CmsContentReferenceSources.ForContentType("species"),
            "300");

        var failures = await validator.ValidateAsync(
            item,
            new ContentTypeDefinition
            {
                Alias = "animal",
                Fields = [CmsDocumentReferenceField()]
            },
            CancellationToken.None);

        await Assert.That(failures).IsEmpty();
        await contentService.Received(1).LoadAsync(
            42,
            300,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Cms_document_reference_rejects_an_entry_from_a_non_public_content_type()
    {
        var contentService = Substitute.For<IContentService>();
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.GetByAliasAsync(
                42,
                "species",
                Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentTypeDefinition, AeroError>(
                new ContentTypeDefinition
                {
                    SiteId = 42,
                    Alias = "species",
                    Name = "Species",
                    AllowPublicUrl = false
                }));
        var validator = new ReferenceExistenceValidator(
            contentService,
            Substitute.For<IDocumentSession>(),
            contentTypeService: contentTypeService);

        var failures = await validator.ValidateAsync(
            CmsContentEntryReferenceItem(
                CmsContentReferenceSources.ForContentType("species"),
                "300"),
            new ContentTypeDefinition
            {
                Alias = "animal",
                Fields = [CmsDocumentReferenceField()]
            },
            CancellationToken.None);

        await Assert.That(failures.Select(failure => failure.ErrorMessage))
            .Contains("The 'species' public content type was not found.");
        await contentService.DidNotReceiveWithAnyArgs()
            .LoadAsync(default, default, default);
    }

    private static ContentItem ItemWithReference(long id) =>
        new()
        {
            SiteId = 1,
            ContentTypeAlias = "species",
            Fields = new Dictionary<string, JsonElement>
            {
                ["classification"] =
                    JsonSerializer.SerializeToElement(id.ToString())
            }
        };

    private static ContentTypeDefinition Definition() =>
        new()
        {
            Alias = "species",
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
                            JsonSerializer.SerializeToElement("taxonomy"),
                        [ReferenceContentFieldSettings.SelectionMode] =
                            JsonSerializer.SerializeToElement(
                                ReferenceContentFieldSettings.SelectionModeHierarchy),
                        [ReferenceContentFieldSettings.SelectLeafOnly] =
                            JsonSerializer.SerializeToElement(true)
                    }
                }
            ]
        };

    private static ContentItem CascadingItem(long speciesId) =>
        new()
        {
            SiteId = 1,
            ContentTypeAlias = "animal",
            Fields = new Dictionary<string, JsonElement>
            {
                ["genus"] = JsonSerializer.SerializeToElement("200"),
                ["species"] = JsonSerializer.SerializeToElement(
                    speciesId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
            }
        };

    private static ContentFieldDefinition ReferenceField(
        string name,
        string target,
        string? dependsOn = null,
        string? targetFilter = null)
    {
        var field = new ContentFieldDefinition
        {
            Name = name,
            Label = name,
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetContentType] =
                    JsonSerializer.SerializeToElement(target)
            }
        };
        if (!string.IsNullOrWhiteSpace(dependsOn))
        {
            field.Settings[ReferenceContentFieldSettings.DependsOnField] =
                JsonSerializer.SerializeToElement(dependsOn);
        }

        if (!string.IsNullOrWhiteSpace(targetFilter))
        {
            field.Settings[ReferenceContentFieldSettings.TargetFilterField] =
                JsonSerializer.SerializeToElement(targetFilter);
        }

        return field;
    }

    private static ContentFieldDefinition CmsDocumentReferenceField() =>
        new()
        {
            Name = "related-page",
            Label = "Related page",
            FieldType = ContentFieldTypes.Reference,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetKind] =
                    JsonSerializer.SerializeToElement(
                        ReferenceContentFieldSettings.TargetKindCmsDocument),
                [ReferenceContentFieldSettings.AllowedSources] =
                    JsonSerializer.SerializeToElement(
                        CmsContentReferenceSources.All.ToArray())
            }
        };

    private static ContentItem CmsContentEntryReferenceItem(
        string source,
        string id) =>
        new()
        {
            SiteId = 42,
            ContentTypeAlias = "animal",
            Fields = new Dictionary<string, JsonElement>
            {
                ["related-page"] = JsonSerializer.SerializeToElement(
                    new CmsContentReferenceValue(source, id),
                    ContentJsonContext.Default.CmsContentReferenceValue)
            }
        };
}
