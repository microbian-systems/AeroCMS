using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PostDocumentSablePersistenceTests
{
    [Test]
    public async Task Strict_post_document_stores_embedded_blocks_with_ids()
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
            Content =
            [
                new MarkdownBlock
                {
                    Id = 81_002,
                    Order = 0,
                    Content = "# Stored inline"
                }
            ]
        });

        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var persistedWithEmbeddedId = await verificationSession.RawQueryAsync<dynamic>(
            "SELECT * FROM post_document WHERE slug = 'embedded-markdown' "
            + "AND content[0].id = 81002 "
            + "AND content[0].blockType = 'markdown' "
            + "AND content[0].content = '# Stored inline';");
        var saved = (await verificationSession.Query<PostDocument>().ToListAsync())
            .SingleOrDefault(post => post.Slug == "embedded-markdown");

        persistedWithEmbeddedId.Count.ShouldBe(1);
        saved.ShouldNotBeNull();
        saved.Content.Count.ShouldBe(1);
        var markdown = saved.Content[0].ShouldBeOfType<MarkdownBlock>();
        markdown.Id.ShouldBe(81_002);
        markdown.Content.ShouldBe("# Stored inline");
    }
}
