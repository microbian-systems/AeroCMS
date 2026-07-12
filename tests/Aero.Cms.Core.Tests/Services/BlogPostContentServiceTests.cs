using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Posts;
using Aero.Core;
using Aero.Core.Http;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public sealed class BlogPostContentServiceTests
{
    private SableTestHarness _harness = null!;
    private IDocumentSession _session = null!;
    private ISiteContext _siteContext = null!;
    private PostContentService _service = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _harness = new SableTestHarness()
            .WithSchema<PostDocument>()
            .WithSchema<ContentSlugDocument>();
        await _harness.InitializeAsync();
        _session = _harness.Session;

        _siteContext = Substitute.For<ISiteContext>();
        _siteContext.SiteId.Returns(42);

        _service = new PostContentService(
            _session,
            _siteContext
        );
    }

    [After(Test)]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    //  Test 1: SaveAsync stamps SiteId from context
    // -----------------------------------------------------------------------
    [Test]
    public async Task SaveAsync_StampsSiteId_FromContext()
    {
        var post = new PostDocument
        {
            Id = Snowflake.NewId(),
            Title = "Test Blog Post",
            Slug = "test-blog-post"
        };

        var result = await _service.SaveAsync(post, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // The SaveAsync stamps SiteId on the post object itself before persisting.
        // Verify the stamped value is correct.
        post.SiteId.ShouldBe(42);
    }

    // -----------------------------------------------------------------------
    //  Test 2: DeleteAsync succeeds when SiteId matches context
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAsync_OwnSite_Succeeds()
    {
        var postId = Snowflake.NewId();

        // Seed the post by using SaveAsync (which creates the document via the service)
        var seedPost = new PostDocument
        {
            Id = postId,
            Title = "Own Blog Post",
            Slug = "own-blog-post",
            SiteId = 42 // matches context
        };
        var saveResult = await _service.SaveAsync(seedPost, CancellationToken.None);
        saveResult.IsSuccess.ShouldBeTrue();

        // Act
        var result = await _service.DeleteAsync(postId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    //  Test 3: DeleteAsync rejects cross-site deletion
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAsync_CrossSite_Rejected()
    {
        var postId = Snowflake.NewId();

        // Use a separate context for seeding so the post is created with SiteId=99
        var otherSiteCtx = Substitute.For<ISiteContext>();
        otherSiteCtx.SiteId.Returns(99);

        var otherService = new PostContentService(_session, otherSiteCtx);

        var seedPost = new PostDocument
        {
            Id = postId,
            Title = "Other Site Blog Post",
            Slug = "other-blog-post"
        };
        var saveResult = await otherService.SaveAsync(seedPost, CancellationToken.None);
        saveResult.IsSuccess.ShouldBeTrue();

        // Act — _service has SiteId=42, the post has SiteId=99
        var result = await _service.DeleteAsync(postId, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}
