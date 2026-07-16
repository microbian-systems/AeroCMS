using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PageEditorPublishingGoldenPathTests
{
    [Test]
    public async Task Public_render_resolves_the_same_token_from_each_pages_site_profile()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var first = CreatePage(9_601, 61, CreateTokenContent("brand-color"));
        first.PublicationState = ContentPublicationState.Published;
        first.PublishedContent = HtmlTreeOperations.ClonePreservingNodeIds(first.DraftContent);
        var second = CreatePage(9_602, 62, CreateTokenContent("brand-color"));
        second.PublicationState = ContentPublicationState.Published;
        second.PublishedContent = HtmlTreeOperations.ClonePreservingNodeIds(second.DraftContent);
        harness.Session.Store(first);
        harness.Session.Store(second);
        await harness.Session.SaveChangesAsync();

        var resolver = Substitute.For<ISiteStyleProfileResolver>();
        resolver.ResolveAsync(61, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile
                {
                    ProfileId = "site-61",
                    ColorTokens = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["brand-color"] = "#112233"
                    }
                })));
        resolver.ResolveAsync(62, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile
                {
                    ProfileId = "site-62",
                    ColorTokens = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["brand-color"] = "#ddeeff"
                    }
                })));

        var firstModel = await RenderPublicPageAsync(harness, first.Id, first.SiteId, resolver);
        var secondModel = await RenderPublicPageAsync(harness, second.Id, second.SiteId, resolver);

        firstModel.RenderedCss.ShouldContain("color: #112233;");
        firstModel.RenderedCss.ShouldNotContain("#ddeeff");
        secondModel.RenderedCss.ShouldContain("color: #ddeeff;");
        secondModel.RenderedCss.ShouldNotContain("#112233");
        await resolver.Received(1).ResolveAsync(61, Arg.Any<CancellationToken>());
        await resolver.Received(1).ResolveAsync(62, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Saved_draft_round_trips_and_public_render_remains_on_the_published_snapshot()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible)
            .WithSchema<ContentSlugDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        const long pageId = 9_501;
        const long siteId = 42;
        var page = CreatePage(pageId, siteId, CreateContent("Initial draft"));
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        await using (var editSession = await harness.OpenSessionAsync())
        {
            var edited = CreatePage(pageId, siteId, CreateContent("Published heading"));
            var saveResult = await CreateContentService(editSession, siteId).SaveAsync(edited);
            saveResult.IsSuccess.ShouldBeTrue();
        }

        await using (var reloadSession = await harness.OpenSessionAsync())
        {
            var reloaded = await reloadSession.LoadAsync<PageDocument>(pageId);
            reloaded.ShouldNotBeNull();
            ReadHeading(reloaded!.DraftContent).ShouldBe("Published heading");
            reloaded.PublishedContent.ShouldBeNull();
        }

        await using (var publishSession = await harness.OpenSessionAsync())
        {
            var publishResult = await CreatePublishingService(publishSession).PublishNowAsync(pageId);
            publishResult.IsSuccess.ShouldBeTrue();
        }

        var publishedModel = await RenderPublicPageAsync(harness, pageId, siteId);
        publishedModel.RenderedMarkup.ShouldContain("Published heading");
        publishedModel.RenderedCss.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");

        await using (var laterEditSession = await harness.OpenSessionAsync())
        {
            var laterEdit = CreatePage(pageId, siteId, CreateContent("Later unpublished draft"));
            var saveResult = await CreateContentService(laterEditSession, siteId).SaveAsync(laterEdit);
            saveResult.IsSuccess.ShouldBeTrue();
        }

        var publicModelAfterLaterEdit = await RenderPublicPageAsync(harness, pageId, siteId);
        publicModelAfterLaterEdit.RenderedMarkup.ShouldContain("Published heading");
        publicModelAfterLaterEdit.RenderedMarkup.ShouldNotContain("Later unpublished draft");

        await using var verificationSession = await harness.OpenSessionAsync();
        var verified = await verificationSession.LoadAsync<PageDocument>(pageId);
        verified.ShouldNotBeNull();
        ReadHeading(verified!.DraftContent).ShouldBe("Later unpublished draft");
        ReadHeading(verified.PublishedContent!).ShouldBe("Published heading");
    }

    private static AeroPageContentService CreateContentService(IDocumentSession session, long siteId)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(siteId);

        return new AeroPageContentService(
            session,
            Substitute.For<IMessageBus>(),
            siteContext,
            NullLogger<AeroPageContentService>.Instance,
            new HtmlContentValidator(
                catalog,
                new HtmlContentModelPolicy(catalog),
                new HtmlAttributePolicy()),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver());
    }

    private static PagePublishingWorkflowService CreatePublishingService(IDocumentSession session)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        return new PagePublishingWorkflowService(
            session,
            Substitute.For<IMessageBus>(),
            new HtmlContentValidator(
                catalog,
                new HtmlContentModelPolicy(catalog),
                new HtmlAttributePolicy()),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            NullLogger<PagePublishingWorkflowService>.Instance);
    }

    private static async Task<DynamicPageModel> RenderPublicPageAsync(
        SableTestHarness harness,
        long pageId,
        long siteId,
        ISiteStyleProfileResolver? styleProfileResolver = null)
    {
        await using var session = await harness.OpenSessionAsync();
        var page = await session.LoadAsync<PageDocument>(pageId);
        page.ShouldNotBeNull();

        var viewModel = new PageViewModel
        {
            Id = page!.Id,
            SiteId = page.SiteId,
            Title = page.Title,
            Slug = page.Slug,
            Path = page.Path,
            Culture = page.Culture,
            IsPublished = page.PublicationState == ContentPublicationState.Published,
            PublicationState = page.PublicationState,
            PublishedOn = page.PublishedOn,
            ShowHeaderNavigation = page.ShowHeaderNavigation,
            HideFooter = page.HideFooter,
            ShowChatAgent = page.ShowChatAgent
        };

        var actor = Substitute.For<IAeroPageActor>();
        actor.GetBySlugAsync(siteId, page.Slug, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(viewModel, null!));
        actor.ListCultureVariantsAsync(pageId, Arg.Any<CancellationToken>())
            .Returns([viewModel]);

        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(siteId);
        var catalog = HtmlElementCatalog.CreateDefault();
        var contentPolicy = new HtmlContentModelPolicy(catalog);
        var attributePolicy = new HtmlAttributePolicy();
        var model = new DynamicPageModel(
            actor,
            siteContext,
            harness.Store,
            new HtmlStaticRenderer(
                catalog,
                contentPolicy,
                attributePolicy,
                new HtmlContentValidator(catalog, contentPolicy, attributePolicy)),
            new NativeCssStyleCompiler(),
            styleProfileResolver ?? CreateStyleProfileResolver(),
            NullLogger<DynamicPageModel>.Instance)
        {
            Slug = page.Slug,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(
                    new EmptyModelMetadataProvider(),
                    new ModelStateDictionary())
            }
        };
        model.Request.Scheme = "https";
        model.Request.Host = new HostString("example.test");

        var result = await model.OnGetAsync();
        result.ShouldBeOfType<PageResult>();
        return model;
    }

    private static HtmlPageContent CreateTokenContent(string tokenName)
    {
        var heading = HtmlNode.CreateElement("h1");
        heading.Children.Add(HtmlNode.CreateText("Site themed heading"));
        heading.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle
            {
                Color = CssColor.Token(tokenName)
            }
        };

        var content = new HtmlPageContent();
        content.Root.Children.Add(heading);
        return content;
    }

    private static ISiteStyleProfileResolver CreateStyleProfileResolver()
    {
        var resolver = Substitute.For<ISiteStyleProfileResolver>();
        resolver.ResolveAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile())));
        return resolver;
    }

    private static PageDocument CreatePage(long id, long siteId, HtmlPageContent content) => new()
    {
        Id = id,
        SiteId = siteId,
        Title = "Golden path page",
        Slug = "golden-path-page",
        Path = "/golden-path-page",
        Culture = "en-US",
        DraftContent = content,
        PublicationState = ContentPublicationState.Draft
    };

    private static HtmlPageContent CreateContent(string headingText)
    {
        var heading = HtmlNode.CreateElement("h1");
        heading.Children.Add(HtmlNode.CreateText(headingText));

        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Nested page body"));

        var grid = HtmlNode.CreateElement("div");
        grid.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Gap = CssLength.Rem(1)
        };
        grid.Children.Add(heading);
        grid.Children.Add(paragraph);

        var section = HtmlNode.CreateElement("section");
        section.Children.Add(grid);

        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        return content;
    }

    private static string? ReadHeading(HtmlPageContent content) =>
        content.Root.Children[0].Children[0].Children[0].Children[0].Text;
}
