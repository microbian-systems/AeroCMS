using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Core.Http;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
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

    private static DynamicPageModel CreateModel(SableTestHarness harness, PageDocument page)
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

        var pageActor = Substitute.For<IAeroPageActor>();
        pageActor
            .GetBySlugAsync(Arg.Any<long>(), page.Slug, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(response);
        pageActor.ListCultureVariantsAsync(page.Id, Arg.Any<CancellationToken>())
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
            renderer,
            new NativeCssStyleCompiler(),
            new NativeStyleProfile(),
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
