using System.Reflection;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using GrainUpdatePageRequest = Aero.Cms.Abstractions.Requests.UpdatePageRequest;
using HttpUpdatePageRequest = Aero.Cms.Abstractions.Http.Clients.UpdatePageRequest;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PagesApiTests
{
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
        actor.UpdateAsync(
                Arg.Any<GrainUpdatePageRequest>(),
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

        using var response = await client.PutAsJsonAsync(
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}",
            request);

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
    }

    private static async Task<WebApplication> CreateAppAsync(IAeroPageActor actor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton(Substitute.For<ISiteContext>());

        var app = builder.Build();
        app.MapPagesApi();
        await app.StartAsync();
        return app;
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
