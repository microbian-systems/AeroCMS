using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Posts.Models;
using Aero.Cms.Modules.Posts.Parsers;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PostImportServiceScopeTests
{
    [Test]
    public async Task ImportAsync_IgnoresPayloadSiteIdAcrossPostsTaxonomyAndSlugs()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PostDocument>()
            .WithSchema<ContentSlugDocument>()
            .WithSchema<Tag>()
            .WithSchema<Series>();
        await harness.InitializeAsync();

        var parser = Substitute.For<IPostImportParser>();
        parser.Supports("posts.json").Returns(true);
        parser.ParseAsync(
                Arg.Any<Stream>(),
                "posts.json",
                Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<List<ImportablePost>, AeroError>(
            [
                new ImportablePost
                {
                    Title = "Scoped import",
                    Slug = "scoped-import",
                    MarkdownContent = "Body",
                    CoverImage = "/cover.webp",
                    Tags = ["security"]
                }
            ]));

        var service = new PostsImportService(
            [parser],
            harness.Session,
            null,
            Substitute.For<ILogger<PostsImportService>>());
        var request = new ImportFileRequest(
            "posts.json",
            "application/json",
            Convert.ToBase64String("x"u8.ToArray()),
            false,
            DuplicateSlugBehavior.Skip,
            null,
            false,
            SiteId: 99);

        var result = await service.ImportAsync(request, authorizedSiteId: 42);

        result.IsSuccess.ShouldBeTrue();
        await using var read = await harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var posts = await read.Query<PostDocument>().ToListAsync();
        var tags = await read.Query<Tag>().ToListAsync();
        var series = await read.Query<Series>().ToListAsync();
        var slugs = await read.Query<ContentSlugDocument>().ToListAsync();

        posts.ShouldHaveSingleItem().SiteId.ShouldBe(42);
        tags.ShouldHaveSingleItem().SiteId.ShouldBe(42);
        series.ShouldHaveSingleItem().SiteId.ShouldBe(42);
        slugs.ShouldHaveSingleItem().SiteId.ShouldBe(42);
    }
}
