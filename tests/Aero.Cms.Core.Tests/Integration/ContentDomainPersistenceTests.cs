using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using FluentAssertions;
using Marten;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public class ContentDomainPersistenceTests
{
    [Test]
    public async Task Page_and_blog_documents_round_trip_with_stable_ids_and_publish_state()
    {
        var session = CreateSession();
        var pageService = new MartenPageContentService(session);
        var blogService = new MartenBlogPostContentService(session);

        var homepage = new PageDocument
        {
            Id = PageDocumentIds.Homepage,
            Kind = PageKind.Homepage,
            Slug = "/",
            Title = "Home",
            Summary = "Welcome to Aero CMS.",
            SeoTitle = "Home | Aero CMS",
            SeoDescription = "Homepage metadata",
            Body = "# Hello world",
            PublicationState = ContentPublicationState.Published
        };

        var blogListing = new PageDocument
        {
            Id = PageDocumentIds.BlogListing,
            Kind = PageKind.BlogListing,
            Slug = "blog",
            Title = "Field Notes",
            Summary = "Latest posts",
            Body = "Latest stories",
            PublicationState = ContentPublicationState.Published
        };

        var id = Snowflake.NewId();
        var post = new BlogPostDocument
        {
            Id = id,
            Slug = "blog/hello-world",
            Title = "Hello World",
            Excerpt = "First post excerpt",
            SeoTitle = "Hello World | Aero CMS",
            SeoDescription = "First post metadata",
            Body = "Post body",
            PublicationState = ContentPublicationState.Draft
        };

        await pageService.SaveAsync(homepage);
        await pageService.SaveAsync(blogListing);
        await blogService.SaveAsync(post);

        var storedHomepage = await pageService.LoadHomepageAsync();
        var storedBlogListing = await pageService.LoadBlogListingAsync();
        var storedPost = await blogService.LoadAsync(post.Id);
        var homepageBySlug = await pageService.FindBySlugAsync("/");
        var postBySlug = await blogService.FindBySlugAsync("blog/hello-world");

        storedHomepage.Should().NotBeNull();
        storedHomepage!.Id.Should().Be(PageDocumentIds.Homepage);
        storedHomepage.IsPubliclyVisible.Should().BeTrue();
        storedHomepage.PublishedOn.Should().NotBeNull();
        storedHomepage.ModifiedOn.Should().BeOnOrAfter(storedHomepage.CreatedOn);
        storedHomepage.SeoTitle.Should().Be("Home | Aero CMS");

        storedBlogListing.Should().NotBeNull();
        storedBlogListing!.Id.Should().Be(PageDocumentIds.BlogListing);
        storedBlogListing.IsPubliclyVisible.Should().BeTrue();

        storedPost.Should().NotBeNull();
        storedPost!.Id.Should().Be(post.Id);
        storedPost.IsPubliclyVisible.Should().BeFalse();
        storedPost.PublishedOn.Should().BeNull();
        storedPost.Excerpt.Should().Be("First post excerpt");

        homepageBySlug.Should().NotBeNull();
        homepageBySlug!.Id.Should().Be(PageDocumentIds.Homepage);

        postBySlug.Should().NotBeNull();
        postBySlug!.Id.Should().Be(post.Id);
    }

    [Test]
    public async Task Duplicate_slug_saves_fail_predictably_for_pages_and_blog_posts()
    {
        var session = CreateSession();
        var pageService = new MartenPageContentService(session);
        var blogService = new MartenBlogPostContentService(session);

        await pageService.SaveAsync(new PageDocument
        {
            Id = PageDocumentIds.Homepage,
            Kind = PageKind.Homepage,
            Slug = "/",
            Title = "Home",
            Body = "Home body",
            PublicationState = ContentPublicationState.Published
        });

        await pageService.SaveAsync(new PageDocument
        {
            Id = PageDocumentIds.BlogListing,
            Kind = PageKind.BlogListing,
            Slug = "blog",
            Title = "Blog",
            Body = "Blog landing",
            PublicationState = ContentPublicationState.Published
        });

        var home2Id = Snowflake.NewId();
        var duplicateHomepageSave = () => pageService.SaveAsync(new PageDocument
        {
            Id = home2Id,
            Kind = PageKind.Standard,
            Slug = "/",
            Title = "Another home",
            Body = "Conflict",
            PublicationState = ContentPublicationState.Draft
        });

        var blogOverviewId = Snowflake.NewId();
        var duplicateBlogSlugSave = () => blogService.SaveAsync(new BlogPostDocument
        {
            Id = blogOverviewId,
            Slug = "blog",
            Title = "Duplicate blog slug",
            Body = "Conflict",
            PublicationState = ContentPublicationState.Published
        });

        var homepageConflict = await duplicateHomepageSave.Should().ThrowAsync<SlugConflictException>();
        homepageConflict.Which.Slug.Should().Be("/");
        homepageConflict.Which.ExistingOwnerId.Should().Be(PageDocumentIds.Homepage.ToString());

        var blogConflict = await duplicateBlogSlugSave.Should().ThrowAsync<SlugConflictException>();
        blogConflict.Which.Slug.Should().Be("blog");
        blogConflict.Which.ExistingOwnerId.Should().Be(PageDocumentIds.BlogListing.ToString());
    }

    private static IDocumentSession CreateSession()
    {
        var pageDocuments = new Dictionary<string, PageDocument>(StringComparer.Ordinal);
        var blogPosts = new Dictionary<string, BlogPostDocument>(StringComparer.Ordinal);
        var slugDocuments = new Dictionary<string, ContentSlugDocument>(StringComparer.Ordinal);

        var session = Substitute.For<IDocumentSession>();

        session.LoadAsync<PageDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                pageDocuments.TryGetValue(callInfo.ArgAt<string>(0), out var page);
                return page;
            });

        session.LoadAsync<BlogPostDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                blogPosts.TryGetValue(callInfo.ArgAt<string>(0), out var post);
                return post;
            });

        session.LoadAsync<ContentSlugDocument>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                slugDocuments.TryGetValue(callInfo.ArgAt<string>(0), out var slugDocument);
                return slugDocument;
            });

        session.When(call => call.Store(Arg.Any<PageDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var page in callInfo.Arg<PageDocument[]>())
                {
                    var clonedPage = Clone(page);
                    pageDocuments[clonedPage.Id.ToString()] = clonedPage;
                }
            });

        session.When(call => call.Store(Arg.Any<BlogPostDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var post in callInfo.Arg<BlogPostDocument[]>())
                {
                    var clonedPost = Clone(post);
                    blogPosts[clonedPost.Id.ToString()] = clonedPost;
                }
            });

        session.When(call => call.Store(Arg.Any<ContentSlugDocument[]>()))
            .Do(callInfo =>
            {
                foreach (var slugDocument in callInfo.Arg<ContentSlugDocument[]>())
                {
                    var clonedSlugDocument = Clone(slugDocument);
                    slugDocuments[clonedSlugDocument.Id.ToString()] = clonedSlugDocument;
                }
            });

        session.When(call => call.Delete(Arg.Any<ContentSlugDocument>()))
            .Do(callInfo =>
            {
                var slugDocument = callInfo.Arg<ContentSlugDocument>();
                slugDocuments.Remove(slugDocument.Id.ToString());
            });

        session.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return session;
    }

    private static PageDocument Clone(PageDocument page)
        => new()
        {
            Id = page.Id,
            Kind = page.Kind,
            Slug = page.Slug,
            Title = page.Title,
            Summary = page.Summary,
            SeoTitle = page.SeoTitle,
            SeoDescription = page.SeoDescription,
            Body = page.Body,
            PublicationState = page.PublicationState,
            CreatedOn = page.CreatedOn,
            ModifiedOn = page.ModifiedOn,
            PublishedOn = page.PublishedOn
        };

    private static BlogPostDocument Clone(BlogPostDocument post)
        => new()
        {
            Id = post.Id,
            Slug = post.Slug,
            Title = post.Title,
            Excerpt = post.Excerpt,
            SeoTitle = post.SeoTitle,
            SeoDescription = post.SeoDescription,
            Body = post.Body,
            PublicationState = post.PublicationState,
            CreatedOn = post.CreatedOn,
            ModifiedOn = post.ModifiedOn,
            PublishedOn = post.PublishedOn
        };

    private static ContentSlugDocument Clone(ContentSlugDocument slugDocument)
        => new()
        {
            Id = slugDocument.Id,
            Slug = slugDocument.Slug,
            NormalizedSlug = slugDocument.NormalizedSlug,
            OwnerId = slugDocument.OwnerId,
            OwnerType = slugDocument.OwnerType
        };
}
