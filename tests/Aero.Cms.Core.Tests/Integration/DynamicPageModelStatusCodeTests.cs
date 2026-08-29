using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Html;
using Aero.Core.Http;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Cms.Modules.Content.Composition;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using System.Text.Json;
using Shouldly;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public async Task Infrastructure_lookup_failure_does_not_fall_through_to_a_route_template()
    {
        var page = CreatePublishedPage(9042);
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetBySlugAsync(
                1,
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel(),
                new PageErrorViewModel
                {
                    Message = "Storage unavailable.",
                    Kind = PageErrorKind.Failure
                }));
        var routeResolver = Substitute.For<IPageRouteTemplateResolver>();
        var model = CreateModel(
            Substitute.For<IDocumentStore>(),
            page,
            actor,
            routeTemplateResolver: routeResolver);
        model.Slug = "catalog/entry-42";

        var result = await model.OnGetAsync();

        result.ShouldBeOfType<StatusCodeResult>().StatusCode
            .ShouldBe(StatusCodes.Status500InternalServerError);
        await routeResolver.DidNotReceive().ResolveAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Published_route_template_resolves_dynamic_entry_and_uses_actual_path_for_urls_and_tags()
    {
        var setup = CreateRouteBoundPublicPage(entryExists: true);

        var result = await setup.Model.OnGetAsync();

        result.ShouldBeOfType<PageResult>();
        setup.Model.RenderedMarkup.ShouldContain("Sample entry");
        setup.Model.PageSlug.ShouldBe("catalog/entry-42");
        setup.Model.CanonicalUrl.ShouldContain("/catalog/entry-42");
        setup.Model.HttpContext.Items["AeroCms.PageSlug"].ShouldBe("catalog/entry-42");
        await setup.Provider.Received(1).FindAsync(
            new ContentViewScope(71, 42),
            "entry-42",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Published_route_template_returns_404_when_dynamic_entry_is_missing()
    {
        var setup = CreateRouteBoundPublicPage(entryExists: false);

        var result = await setup.Model.OnGetAsync();

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Test]
    public async Task Exact_published_route_wins_without_consulting_template_resolver()
    {
        var page = CreatePublishedPage(90_045);
        var actor = Substitute.For<IAeroPageActor>();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<PageDocument>(page.Id, Arg.Any<CancellationToken>()).Returns(page);
        var store = Substitute.For<IDocumentStore>();
        store.LightweightSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        var routeResolver = Substitute.For<IPageRouteTemplateResolver>();
        var model = CreateModel(store, page, actor, routeTemplateResolver: routeResolver);
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.ShouldBeOfType<PageResult>();
        await routeResolver.DidNotReceive().ResolveAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.RedirectResult>().Url.ShouldBe("/nosite");
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.RedirectResult>().Url.ShouldBe("/nosite");
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.StatusCodeResult>().StatusCode
            .ShouldBe(StatusCodes.Status500InternalServerError);
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
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

        result.ShouldBeOfType<PageResult>();
        model.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
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

        result.ShouldBeOfType<PageResult>();
        model.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
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

        result.ShouldBeOfType<PageResult>();
        model.Response.Headers.CacheControl.ToString()
            .ShouldBe("public, no-cache, max-age=0, must-revalidate");
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

        result.ShouldBeOfType<PageResult>();
        model.Response.Headers.CacheControl.ToString().ShouldBe("no-store, no-cache");
        model.Response.Headers.Pragma.ToString().ShouldBe("no-cache");
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

        result.ShouldBeOfType<PageResult>();
        model.DraftId.ShouldBe(page.Id);
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
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

        result.ShouldBeOfType<PageResult>();
        captured.ShouldNotBeNull();
        captured.IsPreview.ShouldBeFalse();
        captured.Source.ShouldNotBeNull();
        captured.Source.VersionId.ShouldBe(source.Value.Id);
        captured.Source.Source.ShouldBe(source.Value.Source);
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

        result.ShouldBeOfType<PageResult>();
        captured.ShouldNotBeNull();
        captured.IsPreview.ShouldBeTrue();
        captured.Source.ShouldNotBeNull();
        captured.Source.VersionId.ShouldBe(draftSource.Value.Id);
        captured.Source.Source.ShouldBe(draftSource.Value.Source);
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
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

        result.ShouldBeOfType<PageResult>();
        model.RenderedMarkup.ShouldContain("Resolved &amp; encoded");
        model.RenderedMarkup.ShouldNotContain("Placeholder");
        model.HttpContext.Items["AeroCms.ContentTypeAliases"]
            .ShouldBeAssignableTo<IReadOnlyList<string>>()
            .ShouldContain("articles");
    }

    [Test]
    public async Task PublicPage_returns_404_when_virtual_content_reference_is_missing()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var page = CreatePublishedPage(9_406);
        var section = HtmlNode.CreateElement("section");
        page.PublishedContent = new HtmlPageContent();
        page.PublishedContent.Root.Children.Add(section);
        page.PublishedComposition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = section.NodeId,
                    ContentEntryKey = new ContentEntryKey("view:catalog", "missing")
                }
            ]
        };
        harness.Session.Store(page);
        await harness.Session.SaveChangesAsync();
        var resolver = Substitute.For<IContentCompositionResolver>();
        resolver.ResolveItemAsync(1, "en-US", Arg.Any<PageContentItemScope>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Fail<PublishedContentItemProjection, AeroError>(
                AeroError.NotFoundError("The query-backed entry was not found.")));
        var model = CreateModel(harness, page, contentResolver: resolver);
        model.Slug = page.Slug;

        var result = await model.OnGetAsync();

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        model.RenderedMarkup.ShouldBeNullOrEmpty();
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

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.StatusCodeResult>().StatusCode
            .ShouldBe(StatusCodes.Status500InternalServerError);
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

        result.ShouldBeOfType<PageResult>();
        await queryResolver.Received(1).ResolveAsync(
            1,
            "en-US",
            Arg.Any<IReadOnlyList<ContentQueryDefinition>>(),
            false,
            Arg.Any<CancellationToken>());
        model.RenderedCulture.ShouldBe("en-US");
    }

    private static DynamicPageModel CreateModel(
        SableTestHarness harness,
        PageDocument page,
        IAeroPageActor? actor = null,
        IContentCompositionResolver? contentResolver = null,
        IPageContentQueryResolver? contentQueryResolver = null,
        IAuthorizationService? authorizationService = null,
        IPageRendererRegistry? rendererRegistry = null,
        IPageRouteTemplateResolver? routeTemplateResolver = null)
        => CreateModel(
            harness.Store,
            page,
            actor,
            contentResolver,
            contentQueryResolver,
            authorizationService,
            rendererRegistry,
            routeTemplateResolver);

    private static DynamicPageModel CreateModel(
        IDocumentStore documentStore,
        PageDocument page,
        IAeroPageActor? actor = null,
        IContentCompositionResolver? contentResolver = null,
        IPageContentQueryResolver? contentQueryResolver = null,
        IAuthorizationService? authorizationService = null,
        IPageRendererRegistry? rendererRegistry = null,
        IPageRouteTemplateResolver? routeTemplateResolver = null)
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
        pageActor.ListCultureVariantsAsync(page.Id, page.SiteId, Arg.Any<CancellationToken>())
            .Returns([vm]);

        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(page.SiteId);

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
            documentStore,
            rendererRegistry ?? new PageRendererRegistry([pageRenderer]),
            contentQueryResolver
                ?? new PageContentQueryResolver(Substitute.For<IContentHierarchyQueryService>()),
            authorizationService,
            NullLogger<DynamicPageModel>.Instance,
            routeTemplateResolver)
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

    private static RouteBoundPageSetup CreateRouteBoundPublicPage(bool entryExists)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var section = catalog.CreateElement("section");
        var title = catalog.CreateElement("h1");
        title.Children.Add(HtmlNode.CreateText("placeholder"));
        section.Children.Add(title);
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        var page = CreatePublishedPage(90_044);
        page.SiteId = 42;
        page.Slug = "catalog-template";
        page.Path = "/catalog-template";
        page.PublishedRouteTemplate = "/catalog/{entryId}";
        page.PublishedContent = content;
        page.PublishedComposition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = section.NodeId,
                    ContentEntryKey = new ContentEntryKey("view:catalog", string.Empty),
                    StableIdRouteParameter = "entryId"
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = title.NodeId,
                    ScopeNodeId = section.NodeId,
                    FieldName = "title"
                }
            ]
        };

        var actor = Substitute.For<IAeroPageActor>();
        actor.GetBySlugAsync(
                42,
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel(),
                new PageErrorViewModel { Message = "Not found.", Kind = PageErrorKind.NotFound }));
        actor.GetByIdAsync(page.Id, 42, Arg.Any<CancellationToken>())
            .Returns(CreateActorResponse(page));

        var routeResolver = Substitute.For<IPageRouteTemplateResolver>();
        routeResolver.ResolveAsync(42, Arg.Any<string>(), "catalog/entry-42", Arg.Any<CancellationToken>())
            .Returns(new PageRouteTemplateMatch(
                page.Id,
                "en-US",
                "/catalog/entry-42",
                new Dictionary<string, string> { ["entryId"] = "entry-42" }));

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<PageDocument>(page.Id, Arg.Any<CancellationToken>()).Returns(page);
        var store = Substitute.For<IDocumentStore>();
        store.LightweightSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.Provider.Returns("view:catalog");
        provider.FindAsync(new ContentViewScope(71, 42), "entry-42", Arg.Any<CancellationToken>())
            .Returns(entryExists
                ? new ContentEntry(
                    new ContentEntryKey("view:catalog", "entry-42"),
                    new ContentViewScope(71, 42),
                    new Dictionary<string, object?> { ["title"] = "Sample entry" })
                : null);
        var providerCatalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        providerCatalog.ResolveAsync(new ContentViewScope(71, 42), "view:catalog", Arg.Any<CancellationToken>())
            .Returns(provider);
        var contentResolver = new ContentCompositionResolver(
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentService>(),
            Substitute.For<IContentQueryService>(),
            [],
            new FixedContentSiteContext(71, 42),
            providerCatalog);
        var model = CreateModel(store, page, actor, contentResolver, routeTemplateResolver: routeResolver);
        model.Slug = "catalog/entry-42";
        model.HttpContext.Request.Scheme = "https";
        model.HttpContext.Request.Host = new HostString("example.test");
        return new RouteBoundPageSetup(model, provider);
    }

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
                new PageErrorViewModel { Message = "Page not found.", Kind = PageErrorKind.NotFound }));
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

    private sealed record RouteBoundPageSetup(
        DynamicPageModel Model,
        IContentEntrySourceProvider Provider);

    private sealed class FixedContentSiteContext(long tenantId, long siteId) : ISiteContext
    {
        public long TenantId { get; } = tenantId;
        public long SiteId { get; } = siteId;
    }
}
