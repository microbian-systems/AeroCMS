using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs;
using Aero.Cms.Modules.Docs.Areas.Docs.Models;
using Aero.Cms.Modules.Docs.Areas.Docs.Pages;
using Aero.Cms.Shared.Components;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;
using System.Globalization;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class DocModelRouteTests
{
    [Test]
    public async Task ApiPath_IsNotMistakenForCulture()
    {
        var docsService = Substitute.For<IDocsService>();
        var treeService = Substitute.For<IDocsTreeService>();
        var page = new DocsPage
        {
            Id = 901,
            SiteId = 42,
            Culture = "en-US",
            Slug = "docs/api/authentication",
            Title = "Authentication",
            MarkdownContent = "# Authentication",
            PublicationState = ContentPublicationState.Published
        };

        docsService
            .GetPublishedBySlugAsync(
                "docs/api/authentication",
                "en-US",
                Arg.Any<CancellationToken>())
            .Returns(new Result<DocsPage?, AeroError>.Ok(page));
        docsService
            .ListCultureVariantsAsync(page.Id, Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<DocsPage>, AeroError>.Ok([page]));
        docsService
            .GetChildrenAsync(page.Id, "en-US", Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<DocsPage>, AeroError>.Ok([]));
        treeService
            .GetSidebarTreeAsync(
                page.SiteId,
                page.Id,
                true,
                "en-US",
                Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<DocsSidebarNode>, AeroError>.Ok([]));
        treeService
            .GetBreadcrumbsAsync(
                page.SiteId,
                page.Id,
                true,
                "en-US",
                Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<DocsPage>, AeroError>.Ok([]));
        treeService.ExtractHeadings(page.MarkdownContent).Returns([]);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 333);

        var model = new DocModel(docsService, treeService)
        {
            Slug = "api/authentication",
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(
                    new EmptyModelMetadataProvider(),
                    new ModelStateDictionary())
            }
        };

        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            var result = await model.OnGetAsync();

            result.ShouldBeOfType<PageResult>();
            model.MarkdownPage.ShouldBeSameAs(page);
            await docsService.Received(1).GetPublishedBySlugAsync(
                "docs/api/authentication",
                "en-US",
                Arg.Any<CancellationToken>());
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
