using System.Collections.Immutable;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Pages;

public sealed class PageContentQueryResolverTests
{
    [Test]
    public async Task Resolver_uses_authoritative_context_and_authoritative_empty_result_alias()
    {
        var hierarchy = Substitute.For<IContentHierarchyQueryService>();
        hierarchy.QueryAsync(
                Arg.Any<ContentQueryRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<ContentQueryRequest>();
                return Task.FromResult<Result<ContentQueryResult>>(
                    new Result<ContentQueryResult>.Ok(
                        new ContentQueryResult(
                            request.Name,
                            "renamed-topics",
                            [],
                            0,
                            false)));
            });
        var resolver = new PageContentQueryResolver(hierarchy);

        var result = await resolver.ResolveAsync(
            42,
            "en-us",
            [
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "old-topics",
                    Traversal = ContentTraversal.Roots,
                    MaximumItems = 25,
                    Projection = ["title"]
                }
            ],
            includeDrafts: true);

        var success = result.ShouldBeOfType<Result<PageContentQueryResolution>.Ok>();
        success.Value.Results.Keys.ShouldBe(["topics"]);
        success.Value.ContentTypeAliases.ShouldBe(["renamed-topics"]);
        await hierarchy.Received(1).QueryAsync(
            Arg.Is<ContentQueryRequest>(request =>
                request.SiteId == 42
                && request.Culture == "en-US"
                && request.ContentTypeId == 501
                && request.ContentTypeAlias == "old-topics"
                && request.IncludeDrafts
                && request.MaximumItems == 25),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Resolver_fails_closed_when_the_content_module_is_unavailable()
    {
        var resolver = new PageContentQueryResolver(
            new UnavailableContentHierarchyQueryService());

        var result = await resolver.ResolveAsync(
            42,
            "en-US",
            [
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "topics",
                    Traversal = ContentTraversal.Roots
                }
            ],
            includeDrafts: false);

        result.ShouldBeOfType<Result<PageContentQueryResolution>.Failure>();
    }

    [Test]
    public async Task Resolver_rejects_invalid_aggregate_before_query_execution()
    {
        var hierarchy = Substitute.For<IContentHierarchyQueryService>();
        var resolver = new PageContentQueryResolver(hierarchy);

        var result = await resolver.ResolveAsync(
            42,
            "en-US",
            [
                Definition("first", 300),
                Definition("second", 300)
            ],
            includeDrafts: false);

        result.ShouldBeOfType<Result<PageContentQueryResolution>.Failure>();
        await hierarchy.DidNotReceive().QueryAsync(
            Arg.Any<ContentQueryRequest>(),
            Arg.Any<CancellationToken>());
    }

    private static ContentQueryDefinition Definition(string name, int maximumItems)
        => new()
        {
            Name = name,
            ContentTypeId = 501,
            ContentTypeAlias = "topics",
            Traversal = ContentTraversal.Roots,
            MaximumItems = maximumItems,
            Projection = ImmutableArray<string>.Empty
        };
}
