using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Areas.Content.Pages;
using Aero.Cms.Modules.Content.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentTypeUrlRendererScopeTests
{
    [Test]
    public async Task Public_page_is_explicitly_anonymous()
    {
        var anonymous = typeof(PublicContentModel)
            .GetCustomAttributes(inherit: true)
            .OfType<IAllowAnonymous>()
            .Any();

        await Assert.That(anonymous).IsTrue();
    }

    [Test]
    public async Task Renderer_rejects_unpublished_item_before_template_rendering()
    {
        var typeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();
        var itemRenderer = Substitute.For<IContentItemRenderer>();
        typeService.GetByAliasAsync(1, "article", Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentTypeDefinition
            {
                Id = 10,
                SiteId = 1,
                Alias = "article",
                Name = "Article",
                AllowPublicUrl = true
            }));
        contentService.GetBySlugAndTypeAsync(
                1,
                "article",
                "en-US",
                "entry",
                Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentItem
            {
                Id = 20,
                SiteId = 1,
                ContentTypeAlias = "article",
                Slug = "entry",
                Culture = "en-US",
                PublicationState = ContentPublicationState.Draft
            }));
        var renderer = new ContentTypeUrlRenderer(typeService, contentService, itemRenderer);

        var result = await renderer.RenderAsync(1, "article", "en-US", "entry");

        await Assert.That(result.IsFailure).IsTrue();
        await itemRenderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    [Test]
    public async Task Renderer_rejects_type_without_public_url_before_item_lookup_or_rendering()
    {
        var typeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();
        var itemRenderer = Substitute.For<IContentItemRenderer>();
        typeService.GetByAliasAsync(1, "article", Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentTypeDefinition
            {
                Id = 10,
                SiteId = 1,
                Alias = "article",
                Name = "Article",
                AllowPublicUrl = false
            }));
        var renderer = new ContentTypeUrlRenderer(typeService, contentService, itemRenderer);

        var result = await renderer.RenderAsync(1, "article", "en-US", "published-entry");

        await Assert.That(result.IsFailure).IsTrue();
        await contentService.DidNotReceiveWithAnyArgs().GetBySlugAndTypeAsync(
            default,
            default!,
            default!,
            default!,
            default);
        await itemRenderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    [Test]
    public async Task Renderer_uses_host_site_for_type_and_item_and_never_falls_through_to_foreign_site()
    {
        var typeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();
        var itemRenderer = Substitute.For<IContentItemRenderer>();
        typeService.GetByAliasAsync(1, "article", Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentTypeDefinition
            {
                Id = 10,
                SiteId = 1,
                Alias = "article",
                Name = "Article",
                AllowPublicUrl = true
            }));
        contentService.GetBySlugAndTypeAsync(
                1,
                "article",
                "en-US",
                "same-slug",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(
                AeroError.NotFoundError("Not found.")));
        contentService.GetBySlugAndTypeAsync(
                2,
                "article",
                "en-US",
                "same-slug",
                Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentItem
            {
                Id = 21,
                SiteId = 2,
                ContentTypeAlias = "article",
                Slug = "same-slug",
                Culture = "en-US",
                PublicationState = ContentPublicationState.Published
            }));
        var renderer = new ContentTypeUrlRenderer(typeService, contentService, itemRenderer);

        var result = await renderer.RenderAsync(1, "article", "en-US", "same-slug");

        await Assert.That(result.IsFailure).IsTrue();
        await typeService.Received(1).GetByAliasAsync(1, "article", Arg.Any<CancellationToken>());
        await contentService.Received(1).GetBySlugAndTypeAsync(
            1,
            "article",
            "en-US",
            "same-slug",
            Arg.Any<CancellationToken>());
        await contentService.DidNotReceive().GetBySlugAndTypeAsync(
            2,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await itemRenderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    private static Task<Result<T, AeroError>> Ok<T>(T value) =>
        Task.FromResult<Result<T, AeroError>>(new Result<T, AeroError>.Ok(value));
}
