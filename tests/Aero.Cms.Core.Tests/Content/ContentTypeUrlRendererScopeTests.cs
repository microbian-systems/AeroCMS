using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content.Areas.Content.Pages;
using Aero.Cms.Modules.Content.Rendering;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

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

    [Test]
    public async Task Renderer_uses_parent_then_default_fallback_and_exposes_requested_and_rendered_cultures()
    {
        var typeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();
        var itemRenderer = Substitute.For<IContentItemRenderer>();
        typeService.GetByAliasAsync(1, "article", Arg.Any<CancellationToken>()).Returns(Ok(new ContentTypeDefinition
        {
            Id = 10, SiteId = 1, Alias = "article", Name = "Article", AllowPublicUrl = true,
            Localization = new() { CultureFallbackPolicy = ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture }
        }));
        contentService.GetBySlugAndTypeAsync(1, "article", "fr-CA", "entry", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(AeroError.NotFoundError("missing")));
        contentService.GetBySlugAndTypeAsync(1, "article", "fr", "entry", Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentItem { Id = 20, SiteId = 1, ContentTypeAlias = "article", Slug = "entry", Culture = "fr", PublicationState = ContentPublicationState.Published }));
        itemRenderer.RenderAsync(Arg.Any<ContentTypeDefinition>(), Arg.Any<ContentItem>(), Arg.Any<CancellationToken>()).Returns(Ok("<p>bonjour</p>"));

        var result = await new ContentTypeUrlRenderer(typeService, contentService, itemRenderer).RenderAsync(1, "article", "fr-CA", "entry", default, "en-US", ["en-US", "fr"]);

        var ok = (Result<PublicContentRenderResult, AeroError>.Ok)result;
        await Assert.That(ok.Value.RequestedCulture).IsEqualTo("fr-CA");
        await Assert.That(ok.Value.RenderedCulture).IsEqualTo("fr");
    }

    [Test]
    public async Task Public_page_builds_absolute_canonical_and_hreflang_urls_from_rendered_variants()
    {
        var typeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();
        var itemRenderer = Substitute.For<IContentItemRenderer>();
        var queryService = Substitute.For<IContentQueryService>();
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(7);
        typeService.GetByAliasAsync(7, "animal", Arg.Any<CancellationToken>()).Returns(Ok(new ContentTypeDefinition
        {
            Id = 10, SiteId = 7, Alias = "animal", Name = "Animal", AllowPublicUrl = true,
            Localization = new() { CultureFallbackPolicy = ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture }
        }));
        contentService.GetBySlugAndTypeAsync(7, "animal", "fr-CA", "loup", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(AeroError.NotFoundError("missing")));
        contentService.GetBySlugAndTypeAsync(7, "animal", "fr", "loup", Arg.Any<CancellationToken>())
            .Returns(Ok(new ContentItem { Id = 20, TranslationGroupId = 20, SiteId = 7, ContentTypeAlias = "animal", Slug = "loup", Culture = "fr", PublicationState = ContentPublicationState.Published }));
        itemRenderer.RenderAsync(Arg.Any<ContentTypeDefinition>(), Arg.Any<ContentItem>(), Arg.Any<CancellationToken>()).Returns(Ok("<p>loup</p>"));
        queryService.ListCultureVariantsAsync(7, "animal", 20, Arg.Any<CancellationToken>()).Returns(Ok<IReadOnlyList<ContentItem>>([
            new() { Id = 21, TranslationGroupId = 20, SiteId = 7, ContentTypeAlias = "animal", Slug = "wolf", Culture = "en-US", PublicationState = ContentPublicationState.Published },
            new() { Id = 20, TranslationGroupId = 20, SiteId = 7, ContentTypeAlias = "animal", Slug = "loup", Culture = "fr", PublicationState = ContentPublicationState.Published }
        ]));
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test", 8443);
        context.Request.PathBase = "/cms";
        context.Request.Path = "/fr-CA/animal/loup";
        context.Features.Set<IAeroSiteSlice>(new AeroSiteSlice { SiteId = 7, DefaultCulture = "en-US", SupportedCultures = ["en-US", "fr", "fr-CA"] });
        var model = new PublicContentModel(siteContext, new ContentTypeUrlRenderer(typeService, contentService, itemRenderer), queryService, NullLogger<PublicContentModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = context }
        };

        var result = await model.OnGetAsync("fr-ca", "animal", "loup", CancellationToken.None);

        result.ShouldBeOfType<PageResult>();
        model.CanonicalUrl.ShouldBe("https://example.test:8443/cms/fr/animal/loup");
        model.AlternateLinks.ShouldContain(link => link.Hreflang == "en-US" && link.Href == "https://example.test:8443/cms/en-US/animal/wolf");
        model.AlternateLinks.ShouldContain(link => link.Hreflang == "fr" && link.Href == "https://example.test:8443/cms/fr/animal/loup");
        model.AlternateLinks.ShouldContain(link => link.Hreflang == "x-default" && link.Href == "https://example.test:8443/cms/en-US/animal/wolf");
        model.ViewData["IsCultureFallback"].ShouldBe(true);
        model.ViewData["DocumentCulture"].ShouldBe("fr");
        model.ViewData["DocumentDirection"].ShouldBe("ltr");
    }

    [Test]
    public async Task Renderer_never_queries_a_fallback_culture_that_is_not_enabled_for_the_site()
    {
        var typeService = Substitute.For<IContentTypeService>();
        var contentService = Substitute.For<IContentService>();
        var itemRenderer = Substitute.For<IContentItemRenderer>();
        typeService.GetByAliasAsync(1, "article", Arg.Any<CancellationToken>()).Returns(Ok(new ContentTypeDefinition
        {
            Id = 10, SiteId = 1, Alias = "article", Name = "Article", AllowPublicUrl = true,
            Localization = new() { CultureFallbackPolicy = ContentCultureFallbackPolicy.ParentCultureThenDefaultCulture }
        }));
        contentService.GetBySlugAndTypeAsync(1, "article", "fr-CA", "entry", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentItem, AeroError>>(AeroError.NotFoundError("missing")));

        var result = await new ContentTypeUrlRenderer(typeService, contentService, itemRenderer)
            .RenderAsync(1, "article", "fr-CA", "entry", default, "en-US", ["en-US"]);

        await Assert.That(result.IsFailure).IsTrue();
        await contentService.DidNotReceive().GetBySlugAndTypeAsync(1, "article", "fr", "entry", Arg.Any<CancellationToken>());
    }

    private static Task<Result<T, AeroError>> Ok<T>(T value) =>
        Task.FromResult<Result<T, AeroError>>(new Result<T, AeroError>.Ok(value));
}
