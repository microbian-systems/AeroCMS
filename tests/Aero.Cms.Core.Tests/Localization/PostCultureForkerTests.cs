using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Posts;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class PostCultureForkerTests
{
    [Test]
    public async Task Fork_CreatesDraftCultureVariant_WithSameTranslationSet()
    {
        var source = new PostDocument
        {
            Id = 100,
            SiteId = 42,
            TranslationGroupId = 900,
            Culture = "en-US",
            Slug = "hello-world",
            Title = "Hello World",
            Excerpt = "Source excerpt",
            SeoTitle = "Source SEO title",
            SeoDescription = "Source SEO description",
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow,
            MarkdownContent = "Source markdown",
            TagIds = [1, 2],
            CategoryIds = [3],
            AuthorId = 4,
            ImageUrl = "/media/source.jpg",
            Likes = 12
        };

        var fork = PostCultureForker.Fork(source, 200, "es-mx", "hola-mundo");

        await Assert.That(fork.Id).IsEqualTo(200);
        await Assert.That(fork.SiteId).IsEqualTo(42);
        await Assert.That(fork.TranslationGroupId).IsEqualTo(900);
        await Assert.That(fork.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Slug).IsEqualTo("hola-mundo");
        await Assert.That(fork.Title).IsEqualTo("Hello World");
        await Assert.That(fork.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(fork.PublishedOn).IsNull();
        await Assert.That(fork.MarkdownContent).IsEqualTo("Source markdown");
        await Assert.That(fork.TagIds).IsEquivalentTo([1L, 2L]);
        await Assert.That(fork.CategoryIds).IsEquivalentTo([3L]);
    }

    [Test]
    public async Task Fork_UsesSourceIdAsTranslationSet_WhenSourceHasNoTranslationSet()
    {
        var source = new PostDocument
        {
            Id = 100,
            SiteId = 42,
            Culture = "en-US",
            Slug = "hello-world",
            Title = "Hello World"
        };

        var fork = PostCultureForker.Fork(source, 200, "ar-SA", "/مرحبا");

        await Assert.That(fork.TranslationGroupId).IsEqualTo(100);
        await Assert.That(fork.Slug).IsEqualTo("مرحبا");
        await Assert.That(fork.Culture).IsEqualTo("ar-SA");
    }
}
