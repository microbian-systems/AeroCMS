using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Core.Http;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authorization;
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
    public async Task PublishedPage_requires_client_revalidation_after_output_cache_eviction()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_402_1);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var model = CreateModel(harness, page);
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.Response.Headers.CacheControl.ToString()
            .Should().Be("public, no-cache, max-age=0, must-revalidate");
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
        model.Response.Headers.CacheControl.ToString().Should().Be("no-store, no-cache");
        model.Response.Headers.Pragma.ToString().Should().Be("no-cache");
        await actor.Received(1).GetByIdAsync(
            page.Id,
            page.SiteId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DraftPreview_UsesRouteValueWhenPropertyBindingHasNotRun()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_423);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns(CreateActorResponse(page));
        actor.ListCultureVariantsAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns([]);
        var model = CreateModel(harness, page, actor);
        model.HttpContext.Request.RouteValues["draftId"] = page.Id.ToString();

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.DraftId.Should().Be(page.Id);
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
    public async Task DraftPreview_WithoutManagerAuthorization_IsForbiddenBeforeLookup()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_404_1);
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var actor = Substitute.For<IAeroPageActor>();
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<object?>(),
                "site:read")
            .Returns(AuthorizationResult.Failed());
        var model = CreateModel(
            harness,
            page,
            actor,
            authorizationService: authorization);
        model.DraftId = page.Id;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
        await actor.DidNotReceive().GetByIdAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublicSourcePage_UsesPublishedSourceVersion()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>()
            .WithSchema<PageSourceVersion>();
        await harness.InitializeAsync();
        var page = CreatePublishedPage(9_404_2);
        page.RendererId = PageRendererIds.Scriban;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        var source = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                page.Id,
                PageRendererIds.Scriban,
                "\r\n<main>published source</main>\n",
                DateTimeOffset.UtcNow));
        page.PublishedSourceVersionId = source.Value.Id;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        PageRenderRequest? captured = null;
        var renderer = CreateSourceRenderer(request => captured = request);
        var model = CreateModel(
            harness,
            page,
            rendererRegistry: CreateSourceRegistry(renderer));
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        captured.Should().NotBeNull();
        captured!.IsPreview.Should().BeFalse();
        captured.Source.Should().NotBeNull();
        captured.Source!.VersionId.Should().Be(source.Value.Id);
        captured.Source.Source.Should().Be(source.Value.Source);
    }

    [Test]
    public async Task AuthorizedDraftSourcePage_UsesDraftSourceVersion()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>()
            .WithSchema<PageSourceVersion>();
        await harness.InitializeAsync();
        var page = CreatePublishedPage(9_404_3);
        page.RendererId = PageRendererIds.Scriban;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        var publishedSource = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                page.Id,
                PageRendererIds.Scriban,
                "<main>published</main>",
                DateTimeOffset.UtcNow));
        var draftSource = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                page.Id,
                PageRendererIds.Scriban,
                "<main>draft</main>",
                DateTimeOffset.UtcNow));
        page.PublishedSourceVersionId = publishedSource.Value.Id;
        page.DraftSourceVersionId = draftSource.Value.Id;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns(CreateActorResponse(page));
        actor.ListCultureVariantsAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns([]);
        PageRenderRequest? captured = null;
        var renderer = CreateSourceRenderer(request => captured = request);
        var model = CreateModel(
            harness,
            page,
            actor,
            rendererRegistry: CreateSourceRegistry(renderer));
        model.DraftId = page.Id;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        captured.Should().NotBeNull();
        captured!.IsPreview.Should().BeTrue();
        captured.Source!.VersionId.Should().Be(draftSource.Value.Id);
        captured.Source.Source.Should().Be(draftSource.Value.Source);
    }

    [Test]
    public async Task PublicSourcePage_WithCrossOwnedSourcePointer_ReturnsNotFound()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>()
            .WithSchema<PageSourceVersion>();
        await harness.InitializeAsync();
        var page = CreatePublishedPage(9_404_4);
        page.RendererId = PageRendererIds.Scriban;
        var sourceStore = new PageSourceVersionStore(harness.Session);
        var foreignSource = (Result<PageSourceVersionSnapshot>.Ok)sourceStore.Stage(
            new PageSourceVersionWriteRequest(
                page.SiteId,
                999,
                PageRendererIds.Scriban,
                "<main>foreign</main>",
                DateTimeOffset.UtcNow));
        page.PublishedSourceVersionId = foreignSource.Value.Id;
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var renderer = CreateSourceRenderer(_ => { });
        var model = CreateModel(
            harness,
            page,
            rendererRegistry: CreateSourceRegistry(renderer));
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        await renderer.DidNotReceive().RenderAsync(
            Arg.Any<PageRenderRequest>(),
            Arg.Any<CancellationToken>());
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

    [Test]
    public async Task PublicPage_returns_500_when_declared_content_query_cannot_resolve()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_421);
        page.PublishedComposition = new PageCompositionDocument
        {
            ContentQueries =
            [
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "topics",
                    Traversal = ContentTraversal.Roots
                }
            ]
        };
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var queryResolver = Substitute.For<IPageContentQueryResolver>();
        queryResolver.ResolveAsync(
                1,
                "en-US",
                Arg.Any<IReadOnlyList<ContentQueryDefinition>>(),
                false,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<PageContentQueryResolution>>(
                new Result<PageContentQueryResolution>.Failure(
                    AeroError.ValidationError(["broken query"]))));
        var model = CreateModel(
            harness,
            page,
            contentQueryResolver: queryResolver);
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        await queryResolver.Received(1).ResolveAsync(
            1,
            "en-US",
            Arg.Is<IReadOnlyList<ContentQueryDefinition>>(queries =>
                queries.Count == 1 && queries[0].Name == "topics"),
            false,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublicPage_uses_reloaded_document_culture_for_content_queries()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_422);
        page.Culture = "en-US";
        page.PublishedComposition = new PageCompositionDocument
        {
            ContentQueries =
            [
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "topics",
                    Traversal = ContentTraversal.Roots
                }
            ]
        };
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();

        var actor = Substitute.For<IAeroPageActor>();
        var queryResolver = Substitute.For<IPageContentQueryResolver>();
        queryResolver.ResolveAsync(
                1,
                "en-US",
                Arg.Any<IReadOnlyList<ContentQueryDefinition>>(),
                false,
                Arg.Any<CancellationToken>())
            .Returns(PageContentQueryResolution.Empty);
        var model = CreateModel(
            harness,
            page,
            actor,
            contentQueryResolver: queryResolver);
        actor.GetBySlugAsync(
                1,
                page.Slug,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel
                {
                    Id = page.Id,
                    SiteId = page.SiteId,
                    Title = page.Title,
                    Slug = page.Slug,
                    Culture = "fr-FR",
                    IsPublished = true
                },
                null!));
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        await queryResolver.Received(1).ResolveAsync(
            1,
            "en-US",
            Arg.Any<IReadOnlyList<ContentQueryDefinition>>(),
            false,
            Arg.Any<CancellationToken>());
        model.RenderedCulture.Should().Be("en-US");
    }

    private static DynamicPageModel CreateModel(
        SableTestHarness harness,
        PageDocument page,
        IAeroPageActor? actor = null,
        IContentCompositionResolver? contentResolver = null,
        IPageContentQueryResolver? contentQueryResolver = null,
        IAuthorizationService? authorizationService = null,
        IPageRendererRegistry? rendererRegistry = null)
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
        var pageRenderer = new AeroCompositionPageRenderer(
            new PageCompositionExpander(
                contentResolver ?? Substitute.For<IContentCompositionResolver>(),
                validator),
            renderer,
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver());
        if (authorizationService is null)
        {
            authorizationService = Substitute.For<IAuthorizationService>();
            authorizationService.AuthorizeAsync(
                    Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                    Arg.Any<object?>(),
                    "site:read")
                .Returns(AuthorizationResult.Success());
        }

        return new DynamicPageModel(
            pageActor,
            siteContext,
            harness.Store,
            rendererRegistry ?? new PageRendererRegistry([pageRenderer]),
            contentQueryResolver
                ?? new PageContentQueryResolver(Substitute.For<IContentHierarchyQueryService>()),
            authorizationService,
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

    private static IPageRenderer CreateSourceRenderer(Action<PageRenderRequest> capture)
    {
        var renderer = Substitute.For<IPageRenderer>();
        renderer.Id.Returns(new PageRendererId(PageRendererIds.Scriban));
        renderer.Descriptor.Returns(new PageRendererDescriptor(
            PageRendererIds.Scriban,
            "Scriban",
            PageEditorKinds.Source,
            SupportsFragments: true,
            IsExperimental: false,
            SourceLanguage: "scriban"));
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capture(call.Arg<PageRenderRequest>());
                return Task.FromResult<Result<RenderedPage>>(
                    new Result<RenderedPage>.Ok(
                        new RenderedPage("<main>rendered</main>", string.Empty, [])));
            });
        return renderer;
    }

    private static IPageRendererRegistry CreateSourceRegistry(IPageRenderer renderer)
    {
        var registry = Substitute.For<IPageRendererRegistry>();
        registry.Resolve(PageRendererIds.Scriban)
            .Returns(new Result<IPageRenderer>.Ok(renderer));
        return registry;
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
