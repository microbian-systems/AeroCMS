using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using AeroDB.Sable;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public class DynamicPageModelStatusCodeTests
{
    [Test]
    public async Task ReExecutedStatusCodePage_preserves_original_status_code()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var model = CreateModel(harness, new PageDocument { Slug = "oops", Title = "Oops" });
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

        var model = CreateModel(harness, new PageDocument { Slug = "oops", Title = "Oops" });
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
            ShowHeaderNavigation = true,
        };

        var response = new AeroRequestResponse<PageViewModel>(vm, null!);

        var pageActor = Substitute.For<IAeroPageActor>();
        pageActor
            .GetBySlugAsync(Arg.Any<long>(), page.Slug, Arg.Any<CancellationToken>())
            .Returns(response);

        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(1L);

        var blockService = Substitute.For<IBlockService>();
        var blockCache = new BlockRenderCache();

        return new DynamicPageModel(pageActor, blockService, blockCache, siteContext, harness.Store)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
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
