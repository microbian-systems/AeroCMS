using System.Reflection;
using System.Net.Http.Json;
using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
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

        await Assert.That(detail.UpdatedAt).IsEqualTo(createdOn.DateTime);
    }

    [Test]
    public async Task UpdateRoutePreservesNestedCompositionThroughOrleansTransport()
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
                        RootNodeJson = captured.RootNodeJson
                    },
                    new PageErrorViewModel());
            });

        await using var app = await CreateAppAsync(actor);
        using var client = app.GetTestClient();
        var compositionRoot = CreateCompositionRoot();
        var rootNodeJson = JsonSerializer.Serialize(
            new NeoPageNode
            {
                NodeId = "page-root",
                CatalogId = "page.root",
                Kind = NeoPageNodeKind.Page,
                Children = [compositionRoot]
            },
            BlockJsonContext.Default.Options);

        var request = new HttpUpdatePageRequest(
            "RTL composition",
            "rtl-composition",
            null,
            null,
            null,
            ContentPublicationState.Draft,
            RootNodeJson: rootNodeJson);

        using var response = await client.PutAsJsonAsync(
            $"/{HttpConstants.ApiPrefix}admin/pages/{pageId}",
            request);

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.RootNodeJson).IsNotNull();

        var deserialized = JsonSerializer.Deserialize<NeoPageNode>(
            captured.RootNodeJson!,
            BlockJsonContext.Default.Options);
        await Assert.That(deserialized).IsNotNull();
        var root = deserialized!.Children.Single();

        await Assert.That(root.Style.Base.Direction)
            .IsEqualTo(ContentDirection.RightToLeft);
        await Assert.That(root.Style.Mobile!.Hidden).IsTrue();
        await Assert.That(root.Children.Single().Properties["text"].GetString())
            .IsEqualTo("مرحبا");
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

    private static NeoPageNode CreateCompositionRoot() =>
        new()
        {
            NodeId = "root",
            CatalogId = "primitive.container",
            Kind = NeoPageNodeKind.Container,
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.RightToLeft
                },
                Mobile = new NodeStyleOverride { Hidden = true }
            },
            Children =
            [
                new NeoPageNode
                {
                    NodeId = "text",
                    CatalogId = "primitive.text",
                    Kind = NeoPageNodeKind.Primitive,
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["text"] = JsonSerializer.SerializeToElement("مرحبا")
                    }
                }
            ]
        };
}
