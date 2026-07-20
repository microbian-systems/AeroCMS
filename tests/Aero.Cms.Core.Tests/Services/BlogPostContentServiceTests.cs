using Aero.Cms.Core.Entities;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Posts.Models;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
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
            .WithSchema<ContentSlugDocument>()
            .WithSchema<Tag>()
            .WithSchema<Category>()
            .WithSchema<Series>();
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

    [Test]
    public async Task FindBySlugAsync_RoundTripsMarkdownContent_InFreshSession()
    {
        var post = new PostDocument
        {
            Id = Snowflake.NewId(),
            Title = "Post with body",
            Slug = "post-with-body",
            MarkdownContent = "# Persisted body",
            PublicationState = ContentPublicationState.Published
        };

        var saveResult = await _service.SaveAsync(post, CancellationToken.None);
        saveResult.IsSuccess.ShouldBeTrue();

        await using var readSession = await _harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var readService = new PostContentService(readSession, _siteContext);

        var result = await readService.FindBySlugAsync(
            post.Slug,
            post.Culture,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var loaded = result is Result<PostDocument?, AeroError>.Ok ok
            ? ok.Value
            : null;
        loaded.ShouldNotBeNull();
        loaded.MarkdownContent.ShouldBe(post.MarkdownContent);
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

    [Test]
    public async Task SaveAsync_ExplicitForeignSite_IsRejected()
    {
        var post = new PostDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 99,
            Title = "Foreign",
            Slug = "foreign"
        };

        var result = await _service.SaveAsync(post, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var persisted = await _session.LoadAsync<PostDocument>(post.Id);
        persisted.ShouldBeNull();
    }

    [Test]
    public async Task SaveAsync_ExistingForeignPost_IsRejectedWithoutMutation()
    {
        var postId = Snowflake.NewId();
        _session.Store(new PostDocument
        {
            Id = postId,
            SiteId = 99,
            Title = "Foreign original",
            Slug = "foreign-original"
        });
        await _session.SaveChangesAsync();

        var result = await _service.SaveAsync(new PostDocument
        {
            Id = postId,
            SiteId = 42,
            Title = "Attempted overwrite",
            Slug = "attempted-overwrite"
        });

        result.IsFailure.ShouldBeTrue();
        await using var readSession = await _harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var persisted = await readSession.LoadAsync<PostDocument>(postId);
        persisted.ShouldNotBeNull();
        persisted.SiteId.ShouldBe(99);
        persisted.Title.ShouldBe("Foreign original");
    }

    [Test]
    public async Task SaveAsync_ForeignTaxonomyReferences_AreRejected()
    {
        var seriesId = Snowflake.NewId();
        var tagId = Snowflake.NewId();
        var categoryId = Snowflake.NewId();
        _session.Store(new Series { Id = seriesId, SiteId = 99, Name = "Other", Slug = "other" });
        _session.Store(new Tag { Id = tagId, SiteId = 99, Name = "Other", Slug = "other" });
        _session.Store(new Category { Id = categoryId, SiteId = 99, Name = "Other", Slug = "other" });
        await _session.SaveChangesAsync();

        var result = await _service.SaveAsync(new PostDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 42,
            Title = "Cross-site relationships",
            Slug = "cross-site-relationships",
            SeriesId = seriesId,
            TagIds = [tagId],
            CategoryIds = [categoryId]
        });

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task SetTranslationGroupPublicationStateAsync_UpdatesOnlyCurrentSite()
    {
        var groupId = Snowflake.NewId();
        var ownId = Snowflake.NewId();
        var foreignId = Snowflake.NewId();
        _session.Store(new PostDocument
        {
            Id = ownId,
            SiteId = 42,
            TranslationGroupId = groupId,
            Title = "Own",
            Slug = "own",
            PublicationState = ContentPublicationState.Draft
        });
        _session.Store(new PostDocument
        {
            Id = foreignId,
            SiteId = 99,
            TranslationGroupId = groupId,
            Title = "Foreign",
            Slug = "foreign",
            PublicationState = ContentPublicationState.Draft
        });
        await _session.SaveChangesAsync();

        var result = await _service.SetTranslationGroupPublicationStateAsync(
            groupId,
            ContentPublicationState.Published);

        result.IsSuccess.ShouldBeTrue();
        await using var readSession = await _harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var own = await readSession.LoadAsync<PostDocument>(ownId);
        var foreign = await readSession.LoadAsync<PostDocument>(foreignId);
        own!.PublicationState.ShouldBe(ContentPublicationState.Published);
        foreign!.PublicationState.ShouldBe(ContentPublicationState.Draft);
    }

    [Test]
    public async Task DeleteTranslationGroupAsync_DeletesOnlyCurrentSiteVariantsAndReservations()
    {
        var groupId = Snowflake.NewId();
        var foreignOnlyGroupId = Snowflake.NewId();
        var ownVariantIds = new[] { Snowflake.NewId(), Snowflake.NewId() };
        var foreignSameGroupId = Snowflake.NewId();
        var foreignOnlyId = Snowflake.NewId();

        foreach (var (id, slug) in ownVariantIds.Zip(new[] { "own-en", "own-fr" }))
        {
            _session.Store(new PostDocument
            {
                Id = id,
                SiteId = 42,
                TranslationGroupId = groupId,
                Title = slug,
                Slug = slug
            });
            _session.Store(ContentSlugDocument.Create(
                slug,
                id,
                ContentSlugOwnerType.BlogPost,
                siteId: 42));
        }

        _session.Store(new PostDocument
        {
            Id = foreignSameGroupId,
            SiteId = 99,
            TranslationGroupId = groupId,
            Title = "Foreign same group",
            Slug = "foreign-same-group"
        });
        var foreignSameGroupReservation = ContentSlugDocument.Create(
            "foreign-same-group",
            foreignSameGroupId,
            ContentSlugOwnerType.BlogPost,
            siteId: 99);
        _session.Store(foreignSameGroupReservation);

        _session.Store(new PostDocument
        {
            Id = foreignOnlyId,
            SiteId = 99,
            TranslationGroupId = foreignOnlyGroupId,
            Title = "Foreign only",
            Slug = "foreign-only"
        });
        var foreignOnlyReservation = ContentSlugDocument.Create(
            "foreign-only",
            foreignOnlyId,
            ContentSlugOwnerType.BlogPost,
            siteId: 99);
        _session.Store(foreignOnlyReservation);
        await _session.SaveChangesAsync();

        await using (var deleteSession = await _harness.OpenSessionAsync())
        {
            var deleteService = new PostContentService(deleteSession, _siteContext);
            var result = await deleteService.DeleteTranslationGroupAsync(groupId);

            result.ShouldBeOfType<Result<int, AeroError>.Ok>().Value.ShouldBe(2);
        }

        await using (var verificationSession = await _harness.OpenSessionAsync(
                         new SessionOptions { Tracking = DocumentTracking.None }))
        {
            foreach (var ownVariantId in ownVariantIds)
            {
                (await verificationSession.LoadAsync<PostDocument>(ownVariantId)).ShouldBeNull();
                var ownReservations = await verificationSession.Query<ContentSlugDocument>()
                    .Where(x =>
                        x.SiteId == 42
                        && x.OwnerId == ownVariantId
                        && x.OwnerType == ContentSlugOwnerType.BlogPost)
                    .ToListAsync();
                ownReservations.ShouldBeEmpty();
            }

            (await verificationSession.LoadAsync<PostDocument>(foreignSameGroupId)).ShouldNotBeNull();
            (await verificationSession.LoadAsync<ContentSlugDocument>(foreignSameGroupReservation.Id)).ShouldNotBeNull();
            (await verificationSession.LoadAsync<PostDocument>(foreignOnlyId)).ShouldNotBeNull();
            (await verificationSession.LoadAsync<ContentSlugDocument>(foreignOnlyReservation.Id)).ShouldNotBeNull();
        }

        await using var foreignOnlyDeleteSession = await _harness.OpenSessionAsync();
        var foreignOnlyDeleteService = new PostContentService(foreignOnlyDeleteSession, _siteContext);
        var foreignOnlyResult = await foreignOnlyDeleteService.DeleteTranslationGroupAsync(foreignOnlyGroupId);

        foreignOnlyResult.ShouldBeOfType<Result<int, AeroError>.Failure>()
            .Error.ShouldBeOfType<AeroError.NotFound>();
    }
}
