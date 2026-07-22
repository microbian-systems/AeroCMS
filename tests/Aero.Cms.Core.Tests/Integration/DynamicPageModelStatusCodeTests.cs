using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Core.Http;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using AeroDB.Sable;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aero.Cms.Core.Tests.Integration;

public class DynamicPageModelStatusCodeTests
{
    [Test]
    public async Task MissingRootHomepage_RedirectsToNoSite()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_396);
        var actor = CreateFailedActor();
        var model = CreateModel(harness, page, actor);

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.RedirectResult>()
            .Which.Url.Should().Be("/nosite");
        await actor.Received(1).GetBySlugAsync(
            page.SiteId,
            "/",
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MissingCultureOnlyRootHomepage_RedirectsToNoSite()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_397);
        var actor = CreateFailedActor();
        var model = CreateModel(harness, page, actor);
        model.Slug = "es-mx";
        model.HttpContext.Items[AeroCultureRoute.CulturePrefixItemKey] = "es-MX";

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.RedirectResult>()
            .Which.Url.Should().Be("/nosite");
        await actor.Received(1).GetBySlugAsync(
            page.SiteId,
            string.Empty,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UnsupportedCultureLikeSlug_RemainsNotFound()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_397_1);
        var actor = CreateFailedActor();
        var model = CreateModel(harness, page, actor);
        model.Slug = "fr-fr";
        model.HttpContext.Items[AeroCultureRoute.CulturePrefixItemKey] = "es-MX";

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        await actor.Received(1).GetBySlugAsync(
            page.SiteId,
            "fr-fr",
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishedRootHomepage_ActorFailure_ReturnsInternalServerError()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_397_2);
        var model = CreateModel(harness, page, CreateFailedActor());
        page.Kind = Aero.Cms.Abstractions.Enums.PageKind.Homepage;
        page.Slug = "/";
        page.Path = "/";
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Test]
    public async Task MissingOrdinarySlug_RemainsNotFound()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_398);
        var model = CreateModel(harness, page, CreateFailedActor());
        model.Slug = "missing-page";

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Test]
    public async Task MissingRootDuringStatusCodeReexecution_RemainsNotFound()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_399);
        var model = CreateModel(harness, page, CreateFailedActor());
        model.PageContext.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(
            new TestStatusCodeReExecuteFeature(404, "/missing-page"));

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Test]
    public async Task ReExecutedStatusCodePage_preserves_original_status_code()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_401);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var model = CreateModel(harness, page);
        model.Slug = "oops";
        model.PageContext.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(
            new TestStatusCodeReExecuteFeature(404, "/missing-page"));

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task DirectPageRequest_keeps_success_status_code()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_402);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var model = CreateModel(harness, page);
        model.Slug = "oops";

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Test]
    public async Task DraftPreview_UsesScopedActorLookup()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_403);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns(CreateActorResponse(page));
        actor.ListCultureVariantsAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns([]);
        var model = CreateModel(harness, page, actor);
        model.DraftId = page.Id;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        await actor.Received(1).GetByIdAsync(
            page.Id,
            page.SiteId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DraftPreview_RejectsDirectDocumentFromAnotherSite()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_404);
        page.SiteId = 99;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(page.Id, 1, Arg.Any<CancellationToken>())
            .Returns(CreateActorResponse(page));
        var model = CreateModel(harness, page, actor);
        model.DraftId = page.Id;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
    }

    [Test]
    public async Task PublicPage_expands_published_typed_content_before_static_rendering()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_405);
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Placeholder"));
        section.Children.Add(paragraph);
        page.PublishedContent = new HtmlPageContent();
        page.PublishedContent.Root.Children.Add(section);
        page.PublishedComposition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = section.NodeId,
                    ContentTypeId = 501,
                    ContentTypeAlias = "articles",
                    ContentItemId = 7_001
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = paragraph.NodeId,
                    ScopeNodeId = section.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ]
        };
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var resolver = Substitute.For<IContentCompositionResolver>();
        resolver.ResolveItemAsync(1, "en-US", Arg.Any<PageContentItemScope>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<PublishedContentItemProjection, AeroError>(new PublishedContentItemProjection
            {
                Id = 7_001,
                ContentTypeAlias = "articles",
                Slug = "aero",
                Culture = "en-US",
                Fields = new Dictionary<string, JsonElement>
                {
                    ["title"] = JsonSerializer.SerializeToElement("Resolved & encoded")
                }
            }));
        var model = CreateModel(harness, page, contentResolver: resolver);
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.RenderedMarkup.Should().Contain("Resolved &amp; encoded");
        model.RenderedMarkup.Should().NotContain("Placeholder");
        model.HttpContext.Items["AeroCms.ContentTypeAliases"]
            .Should().BeAssignableTo<IReadOnlyList<string>>()
            .Which.Should().Contain("articles");
    }

    private static DynamicPageModel CreateModel(
        SableTestHarness harness,
        PageDocument page,
        IAeroPageActor? actor = null,
        IContentCompositionResolver? contentResolver = null)
    {
        var vm = new PageViewModel
        {
            Id = page.Id,
            SiteId = page.SiteId,
            Title = page.Title,
            Slug = page.Slug,
            Culture = page.Culture,
            IsPublished = true,
            ShowHeaderNavigation = true,
        };

        var response = new AeroRequestResponse<PageViewModel>(vm, null!);

        var pageActor = actor ?? Substitute.For<IAeroPageActor>();
        pageActor
            .GetBySlugAsync(Arg.Any<long>(), page.Slug, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(response);
        pageActor.ListCultureVariantsAsync(page.Id, 1, Arg.Any<CancellationToken>())
            .Returns([vm]);

        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(1L);

        var catalog = HtmlElementCatalog.CreateDefault();
        var contentPolicy = new HtmlContentModelPolicy(catalog);
        var attributePolicy = new HtmlAttributePolicy();
        var validator = new HtmlContentValidator(catalog, contentPolicy, attributePolicy);
        var renderer = new HtmlStaticRenderer(catalog, contentPolicy, attributePolicy, validator);

        return new DynamicPageModel(
            pageActor,
            siteContext,
            harness.Store,
            new PageCompositionExpander(
                contentResolver ?? Substitute.For<IContentCompositionResolver>(),
                validator),
            renderer,
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            NullLogger<DynamicPageModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(
                    new EmptyModelMetadataProvider(),
                    new ModelStateDictionary())
            }
        };
    }

    private static AeroRequestResponse<PageViewModel> CreateActorResponse(PageDocument page)
        => new(
            new PageViewModel
            {
                Id = page.Id,
                SiteId = page.SiteId,
                Title = page.Title,
                Slug = page.Slug,
                Culture = page.Culture,
                IsPublished = true,
                ShowHeaderNavigation = true
            },
            new PageErrorViewModel());

    private static IAeroPageActor CreateFailedActor()
    {
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetBySlugAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel(),
                new PageErrorViewModel { Message = "Page not found." }));
        return actor;
    }

    private static ISiteStyleProfileResolver CreateStyleProfileResolver()
    {
        var resolver = Substitute.For<ISiteStyleProfileResolver>();
        resolver.ResolveAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile())));
        return resolver;
    }

    private static PageDocument CreatePublishedPage(long id)
    {
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Rendered content"));
        var content = new HtmlPageContent();
        content.Root.Children.Add(paragraph);

        return new PageDocument
        {
            Id = id,
            SiteId = 1,
            Slug = "oops",
            Path = "/oops",
            Title = "Oops",
            Culture = "en-US",
            PublicationState = Aero.Cms.Abstractions.Enums.ContentPublicationState.Published,
            DraftContent = HtmlTreeOperations.ClonePreservingNodeIds(content),
            PublishedContent = content
        };
    }

    private sealed class TestStatusCodeReExecuteFeature(
        int originalStatusCode,
        string originalPath) : IStatusCodeReExecuteFeature
    {
        public int OriginalStatusCode { get; } = originalStatusCode;
        public string OriginalPathBase { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = originalPath;
        public string? OriginalQueryString { get; set; } = null;
        public Endpoint? Endpoint { get; } = null;
        public RouteValueDictionary? RouteValues { get; } = null;
    }
}
