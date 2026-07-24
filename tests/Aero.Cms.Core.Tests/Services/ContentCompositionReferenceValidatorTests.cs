using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Composition;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class ContentCompositionReferenceValidatorTests
{
    [Test]
    public async Task ValidateAsync_accepts_stable_type_item_query_and_binding_references()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(CreateContentType())));
        contentItems.LoadAsync(42, 7_001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(
                new Result<ContentItem, AeroError>.Ok(CreateContentItem())));
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = 100,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = 101,
                    Query = new PageContentListQuery
                    {
                        SortField = "publishedOn",
                        Filters =
                        [
                            new PageContentFilter
                            {
                                FieldName = "title",
                                Operator = PageContentFilterOperator.Contains,
                                Value = "Aero"
                            }
                        ]
                    }
                }
            ],
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 200,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 7_001
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = 201,
                    ScopeNodeId = 200,
                    FieldName = "title"
                }
            ]
        };
        var validator = new ContentCompositionReferenceValidator(contentTypes, contentItems);

        var result = await validator.ValidateAsync(
            42,
            "en-US",
            composition,
            ContentReferenceValidationMode.Authoring);

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task ValidateAsync_rejects_unknown_fields_wrong_item_types_and_draft_items_at_publish()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(CreateContentType())));
        contentItems.LoadAsync(42, 7_001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(
                new Result<ContentItem, AeroError>.Ok(CreateContentItem(
                    contentTypeAlias: "events",
                    publicationState: ContentPublicationState.Draft))));
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 200,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 7_001
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = 201,
                    ScopeNodeId = 200,
                    FieldName = "missingField"
                }
            ]
        };
        var validator = new ContentCompositionReferenceValidator(contentTypes, contentItems);

        var result = await validator.ValidateAsync(
            42,
            "en-US",
            composition,
            ContentReferenceValidationMode.Publishing);

        result.IsFailure.ShouldBeTrue();
        var failure = (Result<bool, AeroError>.Failure)result;
        var validation = failure.Error.ShouldBeOfType<AeroError.Validation>();
        validation.Errors.ShouldContain(error => error.Contains("missingField", StringComparison.Ordinal));
        validation.Errors.ShouldContain(error => error.Contains("does not belong", StringComparison.Ordinal));
        validation.Errors.ShouldContain(error => error.Contains("must be published", StringComparison.Ordinal));
    }

    [Test]
    public async Task ValidateAsync_resolves_slug_items_with_the_current_type_and_page_culture()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(CreateContentType())));
        contentItems.GetBySlugAndTypeAsync(
                42,
                "articles",
                "fr-FR",
                "bonjour-aero",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(
                new Result<ContentItem, AeroError>.Ok(CreateContentItem(
                    slug: "bonjour-aero",
                    culture: "fr-FR"))));
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 200,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    LookupMode = PageContentItemLookupMode.Slug,
                    Slug = "bonjour-aero"
                }
            ]
        };
        var validator = new ContentCompositionReferenceValidator(contentTypes, contentItems);

        var result = await validator.ValidateAsync(
            42,
            "fr-FR",
            composition,
            ContentReferenceValidationMode.Publishing);

        result.IsSuccess.ShouldBeTrue();
        await contentItems.Received(1).GetBySlugAndTypeAsync(
            42,
            "articles",
            "fr-FR",
            "bonjour-aero",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ValidateAsync_enforces_hierarchy_query_type_projection_root_scope_and_publication()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        var type = CreateContentType();
        type.Structure = ContentStructure.Hierarchical;
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(type)));
        var root = CreateContentItem(
            publicationState: ContentPublicationState.Draft,
            culture: "fr-FR");
        root.SiteId = 99;
        contentItems.LoadAsync(42, 7_001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(
                new Result<ContentItem, AeroError>.Ok(root)));
        var composition = new PageCompositionDocument
        {
            ContentQueries =
            [
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    Traversal = ContentTraversal.Descendants,
                    RootId = 7_001,
                    Projection = ["missingField"]
                }
            ]
        };
        var validator = new ContentCompositionReferenceValidator(contentTypes, contentItems);

        var result = await validator.ValidateAsync(
            42,
            "en-US",
            composition,
            ContentReferenceValidationMode.Publishing);

        var failure = result.ShouldBeOfType<Result<bool, AeroError>.Failure>();
        var validation = failure.Error.ShouldBeOfType<AeroError.Validation>();
        validation.Errors.ShouldContain(error => error.Contains("missingField", StringComparison.Ordinal));
        validation.Errors.ShouldContain(error => error.Contains("current site", StringComparison.Ordinal));
        validation.Errors.ShouldContain(error => error.Contains("culture 'en-US'", StringComparison.Ordinal));
        validation.Errors.ShouldContain(error => error.Contains("must be published", StringComparison.Ordinal));
    }

    private static ContentTypeDefinition CreateContentType() => new()
    {
        Id = 501,
        SiteId = 42,
        Alias = "articles",
        Name = "Articles",
        Fields =
        [
            new ContentFieldDefinition { Name = "title", FieldType = "text" },
            new ContentFieldDefinition { Name = "publishedOn", FieldType = "date" }
        ]
    };

    private static ContentItem CreateContentItem(
        string contentTypeAlias = "articles",
        ContentPublicationState publicationState = ContentPublicationState.Published,
        string slug = "aero-composition",
        string culture = "en-US") => new()
    {
        Id = 7_001,
        SiteId = 42,
        ContentTypeAlias = contentTypeAlias,
        Slug = slug,
        Culture = culture,
        PublicationState = publicationState
    };
}
