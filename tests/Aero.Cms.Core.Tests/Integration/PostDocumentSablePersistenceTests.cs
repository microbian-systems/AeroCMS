using Aero.Cms.Core.Entities;
using Aero.Cms.Abstractions.Enums;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PostDocumentSablePersistenceTests
{
    [Test]
    public async Task Strict_post_document_round_trips_markdown_content()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PostDocument>(SchemaMode.Strict);
        await harness.InitializeAsync();

        harness.Session.Store(new PostDocument
        {
            Id = 81_001,
            SiteId = 42,
            Slug = "embedded-markdown",
            Title = "Embedded Markdown",
            MarkdownContent = "# Stored inline"
        });

        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var saved = (await verificationSession.Query<PostDocument>().ToListAsync())
            .SingleOrDefault(post => post.Slug == "embedded-markdown");

        saved.ShouldNotBeNull();
        saved.MarkdownContent.ShouldBe("# Stored inline");
    }

    [Test]
    public async Task Strict_post_document_queries_seeded_published_content_by_site_and_culture()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PostDocument>(SchemaMode.Strict);
        await harness.InitializeAsync();

        harness.Session.Store(new PostDocument
        {
            Id = 81_002,
            SiteId = 42,
            Culture = "en-US",
            Slug = "published-post",
            Title = "Published Post",
            MarkdownContent = "# Published",
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow
        });
        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var stored = (await verificationSession.Query<PostDocument>().ToListAsync())
            .Single(post => post.Slug == "published-post");
        stored.SiteId.ShouldBe(42);
        stored.Culture.ShouldBe("en-US");
        stored.PublicationState.ShouldBe(ContentPublicationState.Published);

        var storedAsString = await verificationSession.RawQueryAsync<PostDocument>(
            "SELECT * FROM post_document WHERE publication_state = 'Published'");
        var storedAsInteger = await verificationSession.RawQueryAsync<PostDocument>(
            "SELECT * FROM post_document WHERE publication_state = 1");
        storedAsString.Count.ShouldBe(1, $"Integer enum query returned {storedAsInteger.Count} row(s).");

        var matchingSite = await verificationSession.Query<PostDocument>()
            .Where(post => post.SiteId == 42)
            .ToListAsync();
        matchingSite.Select(post => post.Slug).ShouldContain("published-post");

        var matchingCulture = await verificationSession.Query<PostDocument>()
            .Where(post => post.Culture == "en-US")
            .ToListAsync();
        matchingCulture.Select(post => post.Slug).ShouldContain("published-post");

        var matchingPublicationState = await verificationSession.Query<PostDocument>()
            .Where(post => post.PublicationState == ContentPublicationState.Published)
            .ToListAsync();
        matchingPublicationState.Select(post => post.Slug).ShouldContain("published-post");

        var query = verificationSession.Query<PostDocument>()
            .Where(post => post.SiteId == 42 && post.Culture == "en-US")
            .Where(post => post.PublicationState == ContentPublicationState.Published)
            .OrderByDescending(post => post.PublishedOn);

        var latest = await query.Take(3).ToListAsync();
        var paged = await AeroDB.Sable.Pagination.PagedListQueryableExtensions
            .ToPagedListAsync(query, 1, 10);

        latest.Select(post => post.Slug).ShouldContain("published-post");
        paged.Select(post => post.Slug).ShouldContain("published-post");
    }
}
