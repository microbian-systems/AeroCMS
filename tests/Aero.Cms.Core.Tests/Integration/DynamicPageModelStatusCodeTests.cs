using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Areas.Cms.Pages;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Integration;

public class DynamicPageModelStatusCodeTests
{
    [Test]
    public async Task ReExecutedStatusCodePage_preserves_original_status_code()
    {
        var model = CreateModel(new PageDocument { Slug = "oops", Title = "Oops" });
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
        var model = CreateModel(new PageDocument { Slug = "oops", Title = "Oops" });
        model.Slug = "oops";

        var result = await model.OnGetAsync();

        result.Should().BeOfType<PageResult>();
        model.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private static DynamicPageModel CreateModel(PageDocument page)
    {
        var pageService = Substitute.For<IPageContentService>();
        pageService
            .FindBySlugAsync(page.Slug, Arg.Any<CancellationToken>())
            .Returns(new Result<PageDocument?, AeroError>.Ok(page));

        var blockService = Substitute.For<IBlockService>();
        var blockCache = new BlockRenderCache();

        return new DynamicPageModel(pageService, blockService, blockCache)
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
