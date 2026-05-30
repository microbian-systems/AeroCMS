using Aero.Cms.Abstractions.Blocks.Common;
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
            TranslationSetId = 900,
            Culture = "en-US",
            Slug = "hello-world",
            Title = "Hello World",
            Excerpt = "Source excerpt",
            SeoTitle = "Source SEO title",
            SeoDescription = "Source SEO description",
            PublicationState = ContentPublicationState.Published,
            PublishedOn = DateTimeOffset.UtcNow,
            Content =
            [
                new MarkdownBlock
                {
                    Id = 1234,
                    Content = "Source markdown",
                    Order = 0
                }
            ],
            TagIds = [1, 2],
            CategoryIds = [3],
            AuthorId = 4,
            ImageUrl = "/media/source.jpg",
            Likes = 12
        };

        var fork = PostCultureForker.Fork(source, 200, "es-mx", "hola-mundo");

        await Assert.That(fork.Id).IsEqualTo(200);
        await Assert.That(fork.SiteId).IsEqualTo(42);
        await Assert.That(fork.TranslationSetId).IsEqualTo(900);
        await Assert.That(fork.Culture).IsEqualTo("es-MX");
        await Assert.That(fork.Slug).IsEqualTo("hola-mundo");
        await Assert.That(fork.Title).IsEqualTo("Hello World");
        await Assert.That(fork.PublicationState).IsEqualTo(ContentPublicationState.Draft);
        await Assert.That(fork.PublishedOn).IsNull();
        await Assert.That(fork.Content.Count).IsEqualTo(1);
        await Assert.That(fork.Content[0]).IsAssignableTo<MarkdownBlock>();
        await Assert.That(((MarkdownBlock)fork.Content[0]).Content).IsEqualTo("Source markdown");
        await Assert.That(fork.Content[0].Id).IsNotEqualTo(1234);
        await Assert.That(fork.TagIds).IsEqualTo([1, 2]);
        await Assert.That(fork.CategoryIds).IsEqualTo([3]);
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

        await Assert.That(fork.TranslationSetId).IsEqualTo(100);
        await Assert.That(fork.Slug).IsEqualTo("مرحبا");
        await Assert.That(fork.Culture).IsEqualTo("ar-SA");
    }
}
