using System.Reflection;
using System.Net.Http.Json;
using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core.Http;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using GrainUpdatePageRequest = Aero.Cms.Abstractions.Requests.UpdatePageRequest;
using HttpUpdatePageRequest = Aero.Cms.Abstractions.Http.Clients.UpdatePageRequest;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PagesApiTests
{
    [Test]
    public async Task PageEndpointsDeclareExpectedSitePermissionPolicies()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapPagesApi();
        app.MapPagesTreeApi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText!,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
                Policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                    .Select(data => data.Policy)
                    .Where(policy => policy is not null)
                    .ToHashSet(StringComparer.Ordinal)
            })
            .ToList();

        var expected = new (string Method, string Route, string Policy)[]
        {
            ("GET", "/api/v1/admin/pages/", "site:read"),
            ("GET", "/api/v1/admin/pages/{id:long}", "site:read"),
            ("GET", "/api/v1/admin/pages/{id:long}/source", "site:update"),
            ("GET", "/api/v1/admin/pages/slug/{*slug}", "site:read"),
            ("GET", "/api/v1/admin/pages/drafts/{id:long}", "site:read"),
            ("GET", "/api/v1/admin/pages/registered-fragments", "site:read"),
            ("GET", "/api/v1/admin/pages/renderers", "site:read"),
            ("POST", "/api/v1/admin/pages/", "site:create"),
            ("GET", "/api/v1/admin/pages/{id:long}/translations", "site:read"),
            ("POST", "/api/v1/admin/pages/{id:long}/translations", "site:create"),
            ("POST", "/api/v1/admin/pages/{id:long}/ai-translate", "site:update"),
            ("PUT", "/api/v1/admin/pages/{id:long}", "site:update"),
            ("POST", "/api/v1/admin/pages/{id:long}/route-impact", "site:read"),
            ("DELETE", "/api/v1/admin/pages/{id:long}", "site:delete"),
            ("DELETE", "/api/v1/admin/pages/translation-groups/{translationGroupId:long}", "site:delete"),
            ("PUT", "/api/v1/admin/pages/translation-groups/{translationGroupId:long}/publish", "site:update"),
            ("PUT", "/api/v1/admin/pages/translation-groups/{translationGroupId:long}/unpublish", "site:update"),
            ("DELETE", "/api/v1/admin/pages/{id:long}/cascade", "site:delete"),
            ("POST", "/api/v1/admin/pages/delete-multiple", "site:delete"),
            ("PUT", "/api/v1/admin/pages/{id:long}/publish", "site:update"),
            ("PUT", "/api/v1/admin/pages/{id:long}/unpublish", "site:update"),
            ("GET", "/api/v1/admin/preview/pages/{id:long}", "site:read"),
            ("POST", "/api/v1/admin/preview/pages/render-fragment", "site:update"),
            ("GET", "/api/v1/admin/pages/tree/", "site:read"),
            ("GET", "/api/v1/admin/pages/tree/children", "site:read"),
            ("GET", "/api/v1/admin/pages/tree/translation-groups/children", "site:read"),
            ("GET", "/api/v1/admin/pages/tree/navigation", "site:read"),
            ("GET", "/api/v1/admin/pages/tree/breadcrumb/{id:long}", "site:read"),
            ("GET", "/api/v1/admin/pages/tree/ancestors/{id:long}", "site:read"),
            ("PUT", "/api/v1/admin/pages/tree/{id:long}/move", "site:update"),
            ("POST", "/api/v1/admin/pages/tree/compute-path", "site:read"),
            ("GET", "/api/v1/admin/pages/tree/next-order", "site:read")
        };

        foreach (var (method, route, policy) in expected)
        {
            var endpoint = endpoints.Single(candidate =>
                string.Equals(candidate.Route, route, StringComparison.Ordinal)
                && candidate.Methods.Contains(method, StringComparer.Ordinal));
            await Assert.That(endpoint.Policies.Contains(policy)).IsTrue();
        }
    }

    [Test]
    public async Task DraftRazorRouteAloneRequiresSiteReadPermission()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        new PagesModule().ConfigureServices(builder.Services);

        await using var app = builder.Build();
        app.MapRazorPages();
        await app.StartAsync();

        var pageEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>()?.AreaName == "Cms")
            .ToList();

        var preview = pageEndpoints.Single(endpoint => string.Equals(
            endpoint.RoutePattern.RawText?.TrimStart('/'),
            "_cms/preview/pages/drafts/{draftId:long}",
            StringComparison.OrdinalIgnoreCase));
        await Assert.That(preview.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(data => string.Equals(data.Policy, "site:read", StringComparison.Ordinal))).IsTrue();

        var publicSelectors = pageEndpoints
            .Where(endpoint => endpoint.RoutePattern.RawText is "/" or "{**slug}")
            .ToList();
        await Assert.That(publicSelectors).IsNotEmpty();
        await Assert.That(publicSelectors.All(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)).IsTrue();
    }

    [Test]
    public async Task DraftRazorRouteWinsOverPublicCatchAll()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        new PagesModule().ConfigureServices(builder.Services);

        await using var app = builder.Build();
        app.UseRouting();
        app.Use((
            Microsoft.AspNetCore.Http.HttpContext context,
            Microsoft.AspNetCore.Http.RequestDelegate _) =>
        {
            context.Response.Headers["X-Test-Draft-Id"] =
                context.Request.RouteValues["draftId"]?.ToString() ?? string.Empty;
            context.Response.Headers["X-Test-Slug"] =
                context.Request.RouteValues["slug"]?.ToString() ?? string.Empty;
            context.Response.StatusCode = 204;
            return Task.CompletedTask;
        });
        app.MapRazorPages();
        await app.StartAsync();

        using var response = await app.GetTestClient()
            .GetAsync("/_cms/preview/pages/drafts/123");

        await Assert.That(response.Headers.GetValues("X-Test-Draft-Id").Single()).IsEqualTo("123");
        await Assert.That(response.Headers.GetValues("X-Test-Slug").Single()).IsEmpty();
    }

    [Test]
    public async Task TreeHasChildren_IgnoresChildrenFromAnotherSite()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var parent = new PageDocument
        {
            Id = 810,
            SiteId = 42,
            Title = "Parent",
            Slug = "parent",
            Path = "/parent"
        };
        harness.Session.Store(parent);
        harness.Session.Store(new PageDocument
        {
            Id = 811,
            SiteId = 99,
            ParentId = parent.Id,
            Title = "Foreign child",
            Slug = "foreign-child",
            Path = "/parent/foreign-child"
        });
        await harness.Session.SaveChangesAsync();
        var mapper = typeof(PagesTreeApi).GetMethod(
            "MapToTreeItemsAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        await Assert.That(mapper).IsNotNull();

        var task = (Task<List<object>>)mapper!.Invoke(
            null,
            new object[] { harness.Session, new List<PageDocument> { parent }, 42L, CancellationToken.None })!;
        var item = (await task).Single();
        var hasChildren = (bool)item.GetType().GetProperty("HasChildren")!.GetValue(item)!;

        await Assert.That(hasChildren).IsFalse();
    }

    [Test]
    public async Task MapToDetail_UsesCreatedOnWhenModifiedOnIsMissing()
    {
        var createdOn = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);
        var page = new PageDocument
        {
            Id = 1501703887826436096,
            SiteId = 1501703887469527040,
            Title = "Seeded page",
            Slug = "seeded-page",
            CreatedOn = createdOn,
            ModifiedOn = null,
            PublicationState = ContentPublicationState.Published
        };

        var mapper = typeof(PagesApi).GetMethod(
            "MapToDetail",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(PageDocument)]);

        await Assert.That(mapper).IsNotNull();

        var detail = (PageDetail)mapper!.Invoke(null, [page])!;

        await Assert.That(detail.SiteId).IsEqualTo(page.SiteId);
        await Assert.That(detail.UpdatedAt).IsEqualTo(createdOn.DateTime);
    }

    [Test]
    public async Task UpdateRoutePreservesNestedHtmlContentThroughOrleansTransport()
    {
        const long pageId = 601;
        GrainUpdatePageRequest? captured = null;
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(pageId, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel { Id = pageId, SiteId = 42 },
                new PageErrorViewModel()));
        actor.UpdateAsync(
                Arg.Any<GrainUpdatePageRequest>(),
                42,
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<GrainUpdatePageRequest>();
                return new AeroRequestResponse<PageViewModel>(
                    new PageViewModel
                    {
                        Id = pageId,
                        SiteId = 42,
                        Title = captured.Title,
                        Slug = captured.Slug,
                        PublicationState = captured.PublicationState,
                        DraftContentJson = captured.DraftContentJson
                    },
                    new PageErrorViewModel());
            });

        await using var app = await CreateAppAsync(actor);
        using var client = app.GetTestClient();
        var content = CreateHtmlContent();

        var request = new HttpUpdatePageRequest(
            "RTL composition",
            "rtl-composition",
            null,
            null,
            null,
            ContentPublicationState.Draft,
            DraftContent: content);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.WithTestUser(42);
        using var response = await client.SendAsync(httpRequest);

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.DraftContentJson).IsNotNull();

        var deserialized = System.Text.Json.JsonSerializer.Deserialize(
            captured.DraftContentJson!,
            HtmlJsonContext.Default.HtmlPageContent);
        await Assert.That(deserialized).IsNotNull();
        var section = deserialized!.Root.Children.Single();
        var paragraph = section.Children.Single().Children.Single();

        await Assert.That(section.Attributes["dir"]).IsEqualTo("rtl");
        await Assert.That(section.Style!.Display).IsEqualTo(CssDisplay.Grid);
        await Assert.That(section.Style.GridColumns).IsEqualTo(2);
        await Assert.That(paragraph.Children.Single().Text).IsEqualTo("مرحبا");
        await actor.Received(1).UpdateAsync(
            Arg.Any<GrainUpdatePageRequest>(),
            42,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetById_CrossSiteActorMiss_ReturnsNotFoundAndUsesCurrentSite()
    {
        const long pageId = 602;
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(pageId, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel(),
                new PageErrorViewModel { Message = "Page not found" }));

        await using var app = await CreateAppAsync(actor);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}");
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
        await actor.Received(1).GetByIdAsync(pageId, 42, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSource_ReturnsExactManagerSourceAndUsesCurrentSite()
    {
        const long pageId = 603;
        const string exactSource = "\r\n<main>{{ page.title }}</main>\n";
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetSourceAsync(pageId, 42, Arg.Any<CancellationToken>())
            .Returns(new PageSourceViewModel(
                9001,
                PageRendererIds.Scriban,
                "source-hash",
                exactSource));
        await using var app = await CreateAppAsync(actor);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}/source");
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        var source = await response.Content.ReadFromJsonAsync<PageSourceViewModel>();
        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Source).IsEqualTo(exactSource);
        await actor.Received(1).GetSourceAsync(
            pageId,
            42,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSource_MissingOrCrossOwnedSourceReturnsNotFound()
    {
        const long pageId = 604;
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetSourceAsync(pageId, 42, Arg.Any<CancellationToken>())
            .Returns((PageSourceViewModel?)null);
        await using var app = await CreateAppAsync(actor);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}/source");
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UnsavedScribanPreview_UsesServerContextAndExactUnpersistedSource()
    {
        const string exactSource = "\r\n<main>{{ page.title }}</main>\n";
        PageRenderRequest? captured = null;
        var renderer = Substitute.For<IPageRenderer>();
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<PageRenderRequest>();
                return Task.FromResult<Result<RenderedPage>>(
                    new Result<RenderedPage>.Ok(
                        new RenderedPage("<main>preview</main>", ".preview{}", [])));
            });
        var registry = Substitute.For<IPageRendererRegistry>();
        registry.Resolve(PageRendererIds.Scriban)
            .Returns(new Result<IPageRenderer>.Ok(renderer));
        var pageService = Substitute.For<IPageContentService>();
        var queryResolver = Substitute.For<IPageContentQueryResolver>();
        queryResolver.ResolveAsync(
                42,
                "en-US",
                Arg.Any<IReadOnlyList<ContentQueryDefinition>?>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(PageContentQueryResolution.Empty);
        await using var app = await CreateAppAsync(
            Substitute.For<IAeroPageActor>(),
            registry,
            pageService,
            queryResolver);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/preview/pages/render-fragment")
        {
            Content = JsonContent.Create(new PreviewPageFragmentRequest(
                Content: null,
                Culture: "en-us",
                RendererId: PageRendererIds.Scriban,
                Title: "Unsaved source page",
                Slug: "unsaved-source-page",
                Path: "/unsaved-source-page",
                Source: exactSource))
        };
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        var payload = await response.Content.ReadFromJsonAsync<PreviewPageFragmentResponse>();
        await Assert.That(payload).IsNotNull();
        await Assert.That(payload!.Html).Contains("<style data-aero-page-styles>");
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Metadata.SiteId).IsEqualTo(42);
        await Assert.That(captured.Metadata.Culture).IsEqualTo("en-US");
        await Assert.That(captured.IsPreview).IsTrue();
        await Assert.That(captured.Source).IsNotNull();
        await Assert.That(captured.Source!.VersionId).IsEqualTo(0);
        await Assert.That(captured.Source.Source).IsEqualTo(exactSource);
        await Assert.That(captured.Source.SourceHash)
            .IsEqualTo("ac64d4947922fd4b9c43e6225150c58df3bd0235e63c5e5a756a9b58af5fc1b0");
    }

    [Test]
    public async Task UnsavedPreview_ReturnsActionableValidationMessage()
    {
        var renderer = Substitute.For<IPageRenderer>();
        renderer.RenderAsync(Arg.Any<PageRenderRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<RenderedPage>>(
                new Result<RenderedPage>.Failure(
                    AeroError.ValidationError(
                        ["The '<style>' element is not supported in page fragments."]))));
        var registry = Substitute.For<IPageRendererRegistry>();
        registry.Resolve(PageRendererIds.Htmx)
            .Returns(new Result<IPageRenderer>.Ok(renderer));
        var queryResolver = Substitute.For<IPageContentQueryResolver>();
        queryResolver.ResolveAsync(
                42,
                "en-US",
                Arg.Any<IReadOnlyList<ContentQueryDefinition>?>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(PageContentQueryResolution.Empty);
        await using var app = await CreateAppAsync(
            Substitute.For<IAeroPageActor>(),
            registry,
            Substitute.For<IPageContentService>(),
            queryResolver);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/preview/pages/render-fragment")
        {
            Content = JsonContent.Create(new PreviewPageFragmentRequest(
                Content: null,
                Culture: "en-US",
                RendererId: PageRendererIds.Htmx,
                Source: "<style>.card{display:block}</style><div class=\"card\"></div>"))
        };
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
        await Assert.That(payload.RootElement.GetProperty("error").GetString())
            .IsEqualTo("The '<style>' element is not supported in page fragments.");
    }

    [Test]
    public async Task ExistingPagePreview_WithDifferentRenderer_ReturnsConflict()
    {
        const long pageId = 605;
        var renderer = Substitute.For<IPageRenderer>();
        var registry = Substitute.For<IPageRendererRegistry>();
        registry.Resolve(PageRendererIds.Scriban)
            .Returns(new Result<IPageRenderer>.Ok(renderer));
        var pageService = Substitute.For<IPageContentService>();
        pageService.LoadAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Result<PageDocument?, AeroError>.Ok(new PageDocument
            {
                Id = pageId,
                SiteId = 42,
                RendererId = PageRendererIds.AeroComposition
            }));
        await using var app = await CreateAppAsync(
            Substitute.For<IAeroPageActor>(),
            registry,
            pageService,
            Substitute.For<IPageContentQueryResolver>());
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/preview/pages/render-fragment")
        {
            Content = JsonContent.Create(new PreviewPageFragmentRequest(
                Content: null,
                RendererId: PageRendererIds.Scriban,
                PageId: pageId,
                Source: "<main>preview</main>"))
        };
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.Conflict);
        await renderer.DidNotReceive().RenderAsync(
            Arg.Any<PageRenderRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_WithCrossSiteParent_ReturnsNotFoundBeforeMutation()
    {
        const long parentId = 701;
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(parentId, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel(),
                new PageErrorViewModel { Message = "Page not found" }));
        await using var app = await CreateAppAsync(actor);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/pages/")
        {
            Content = JsonContent.Create(new Aero.Cms.Abstractions.Http.Clients.CreatePageRequest(
                "Child",
                "child",
                null,
                null,
                null,
                ContentPublicationState.Draft,
                ParentId: parentId))
        };
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
        await actor.DidNotReceive().CreateAsync(
            Arg.Any<Aero.Cms.Abstractions.Requests.CreatePageRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_WithCrossSiteParent_ReturnsNotFoundBeforeMutation()
    {
        const long pageId = 702;
        const long parentId = 703;
        var actor = Substitute.For<IAeroPageActor>();
        actor.GetByIdAsync(pageId, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel { Id = pageId, SiteId = 42 },
                new PageErrorViewModel()));
        actor.GetByIdAsync(parentId, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<PageViewModel>(
                new PageViewModel(),
                new PageErrorViewModel { Message = "Page not found" }));
        await using var app = await CreateAppAsync(actor);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}")
        {
            Content = JsonContent.Create(new HttpUpdatePageRequest(
                "Child",
                "child",
                null,
                null,
                null,
                ContentPublicationState.Draft,
                ParentId: parentId))
        };
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
        await actor.DidNotReceive().UpdateAsync(
            Arg.Any<GrainUpdatePageRequest>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteMultiple_MixedSiteActorMiss_ReturnsNotFound()
    {
        var ids = new[] { 704L, 705L };
        var actor = Substitute.For<IAeroPageActor>();
        actor.DeleteMultipleAsync(
                Arg.Is<long[]>(values => values.SequenceEqual(ids)),
                42,
                false,
                Arg.Any<CancellationToken>())
            .Returns(new PageBulkDeleteActorResult { NotFound = true });
        await using var app = await CreateAppAsync(actor);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{HttpConstants.ApiPrefix}admin/pages/delete-multiple")
        {
            Content = JsonContent.Create(new DeleteMultiplePagesRequest(ids))
        };
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);
    }

    [Test]
    public async Task PublishTranslationGroup_RoutesBatchThroughWorkflowWithAuthorizedSite()
    {
        const long translationGroupId = 7_500;
        var variants = new PageDocument[]
        {
            new()
            {
                Id = 7_501,
                SiteId = 42,
                TranslationGroupId = translationGroupId,
                Culture = "en-US",
                Title = "English",
                Slug = "english"
            },
            new()
            {
                Id = 7_502,
                SiteId = 42,
                TranslationGroupId = translationGroupId,
                Culture = "fr-FR",
                Title = "French",
                Slug = "french"
            }
        };
        var pageService = Substitute.For<IPageContentService>();
        pageService.ListCultureVariantsAsync(
                translationGroupId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IReadOnlyList<PageDocument>, AeroError>>(
                new Result<IReadOnlyList<PageDocument>, AeroError>.Ok(variants)));
        var publishingWorkflow = Substitute.For<IPagePublishingWorkflowService>();
        publishingWorkflow.PublishBatchAsync(
                Arg.Any<IReadOnlyCollection<long>>(),
                42,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<bool, AeroError>>(
                new Result<bool, AeroError>.Ok(true)));
        await using var app = await CreateAppAsync(
            Substitute.For<IAeroPageActor>(),
            pageService: pageService,
            publishingWorkflow: publishingWorkflow);
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/{HttpConstants.ApiPrefix}admin/pages/translation-groups/{translationGroupId}/publish");
        request.WithTestUser(42);

        using var response = await app.GetTestClient().SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.OK);
        await publishingWorkflow.Received(1).PublishBatchAsync(
            Arg.Is<IReadOnlyCollection<long>>(ids =>
                ids.SequenceEqual(variants.Select(page => page.Id))),
            42,
            Arg.Any<CancellationToken>());
        await pageService.DidNotReceive().SaveAsync(
            Arg.Any<PageDocument>(),
            Arg.Any<CancellationToken>());
    }

    private static async Task<WebApplication> CreateAppAsync(
        IAeroPageActor actor,
        IPageRendererRegistry? rendererRegistry = null,
        IPageContentService? pageService = null,
        IPageContentQueryResolver? contentQueryResolver = null,
        IPagePublishingWorkflowService? publishingWorkflow = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(actor);
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(42);
        builder.Services.AddSingleton(siteContext);
        builder.Services.AddSingleton<IPageRendererRegistry>(
            rendererRegistry ?? CreateRendererRegistry());
        builder.Services.AddSingleton(
            pageService ?? Substitute.For<IPageContentService>());
        builder.Services.AddSingleton(
            contentQueryResolver ?? Substitute.For<IPageContentQueryResolver>());
        builder.Services.AddSingleton(
            publishingWorkflow ?? Substitute.For<IPagePublishingWorkflowService>());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPagesApi();
        await app.StartAsync();
        return app;
    }

    private static IPageRendererRegistry CreateRendererRegistry()
    {
        var renderer = Substitute.For<IPageRenderer>();
        renderer.Id.Returns(new PageRendererId(PageRendererIds.AeroComposition));
        renderer.Descriptor.Returns(new PageRendererDescriptor(
            PageRendererIds.AeroComposition,
            "Aero",
            PageEditorKinds.VisualComposition,
            SupportsFragments: true,
            IsExperimental: false));
        return new PageRendererRegistry([renderer]);
    }

    private static HtmlPageContent CreateHtmlContent()
    {
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("مرحبا"));

        var container = HtmlNode.CreateElement("div");
        container.Children.Add(paragraph);

        var section = HtmlNode.CreateElement("section");
        section.Attributes["dir"] = "rtl";
        section.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2
        };
        section.Children.Add(container);

        var content = new HtmlPageContent();
        content.Root.Children.Add(section);
        return content;
    }
}
