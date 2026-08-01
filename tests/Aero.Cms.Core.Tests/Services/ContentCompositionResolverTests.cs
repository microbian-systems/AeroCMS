using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Composition;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class ContentCompositionResolverTests
{
    [Test]
    public async Task ResolveListAsync_applies_publication_culture_filter_sort_and_page_boundaries()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        var contentQueries = Substitute.For<IContentQueryService>();
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentTypeDefinition, AeroError>(ContentType("current-articles")));
        var candidates = new[]
        {
            Item(1, "current-articles", "en-US", ContentPublicationState.Published, "Aero first", 10),
            Item(2, "current-articles", "en-US", ContentPublicationState.Published, "Aero second", 20),
            Item(3, "current-articles", "fr-FR", ContentPublicationState.Published, "Aero French", 30),
            Item(4, "current-articles", "en-US", ContentPublicationState.Draft, "Aero draft", 40),
            Item(5, "current-articles", "en-US", ContentPublicationState.Published, "Other", 50)
        };
        contentQueries.GetByTypeAsync(42, "current-articles", 0, 1_001, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>(
                (candidates, candidates.Length)));
        var scope = new PageContentListScope
        {
            NodeId = 100,
            ContentTypeId = 501,
            ContentTypeAlias = "stale-alias",
            TemplateRootNodeId = 101,
            Query = new PageContentListQuery
            {
                PageSize = 1,
                SortField = "priority",
                SortDirection = PageContentSortDirection.Descending,
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
        };
        var resolver = new ContentCompositionResolver(contentTypes, contentItems, contentQueries);

        var result = await resolver.ResolveListAsync(42, "en-us", scope, pageNumber: 2);

        result.IsSuccess.ShouldBeTrue();
        var page = ((Result<Aero.Cms.Abstractions.Content.Composition.PublishedContentPage, AeroError>.Ok)result).Value;
        page.TotalCount.ShouldBe(2);
        page.PageNumber.ShouldBe(2);
        page.Items.Single().Id.ShouldBe(1);
    }

    [Test]
    public async Task ResolveItemAsync_rejects_draft_or_wrong_culture_items()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        var contentQueries = Substitute.For<IContentQueryService>();
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentTypeDefinition, AeroError>(ContentType("articles")));
        contentItems.LoadAsync(42, 9, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentItem, AeroError>(
                Item(9, "articles", "fr-FR", ContentPublicationState.Draft, "Bonjour", 1)));
        var resolver = new ContentCompositionResolver(contentTypes, contentItems, contentQueries);

        var result = await resolver.ResolveItemAsync(
            42,
            "en-US",
            new PageContentItemScope
            {
                NodeId = 200,
                ContentTypeId = 501,
                ContentTypeAlias = "articles",
                ContentItemId = 9
            });

        result.IsFailure.ShouldBeTrue();
        ((Result<Aero.Cms.Abstractions.Content.Composition.PublishedContentItemProjection, AeroError>.Failure)result)
            .Error.ShouldBeOfType<AeroError.NotFound>();
    }

    [Test]
    public async Task ResolveListAsync_fails_closed_above_the_candidate_bound()
    {
        var contentTypes = Substitute.For<IContentTypeService>();
        var contentItems = Substitute.For<IContentService>();
        var contentQueries = Substitute.For<IContentQueryService>();
        contentTypes.GetByIdAsync(42, 501, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<ContentTypeDefinition, AeroError>(ContentType("articles")));
        contentQueries.GetByTypeAsync(42, "articles", 0, 1_001, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>(([], 1_001)));
        var resolver = new ContentCompositionResolver(contentTypes, contentItems, contentQueries);

        var result = await resolver.ResolveListAsync(
            42,
            "en-US",
            new PageContentListScope
            {
                NodeId = 100,
                ContentTypeId = 501,
                ContentTypeAlias = "articles",
                TemplateRootNodeId = 101
            },
            pageNumber: 1);

        result.IsFailure.ShouldBeTrue();
        var error = ((Result<Aero.Cms.Abstractions.Content.Composition.PublishedContentPage, AeroError>.Failure)result).Error;
        error.ShouldBeOfType<AeroError.Validation>().Errors
            .ShouldContain(message => message.Contains("bounded render query limit", StringComparison.Ordinal));
    }

    private static ContentTypeDefinition ContentType(string alias) => new()
    {
        Id = 501,
        SiteId = 42,
        Alias = alias,
        Name = "Articles"
    };

    private static ContentItem Item(
        long id,
        string alias,
        string culture,
        ContentPublicationState state,
        string title,
        int priority) => new()
    {
        Id = id,
        SiteId = 42,
        ContentTypeAlias = alias,
        Slug = $"item-{id}",
        Culture = culture,
        PublicationState = state,
        PublishedOn = DateTimeOffset.UtcNow.AddMinutes(-id),
        Fields = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement(title),
            ["priority"] = JsonSerializer.SerializeToElement(priority)
        }
    };
}
