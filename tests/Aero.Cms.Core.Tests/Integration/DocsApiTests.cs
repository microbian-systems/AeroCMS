using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Modules.Docs.Areas.Api.v1;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class DocsApiTests
{
    [Test]
    public async Task DocsEndpoints_DeclareAllExpectedSitePolicies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAeroDocsActor>(_ => null!);
        builder.Services.AddSingleton<ISiteContext>(_ => null!);
        await using var app = builder.Build();
        app.MapDocsApi();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
        var expected = new (string Method, string Route, string Policy)[]
        {
            ("GET", "/api/v1/admin/docs/", "site:read"), ("GET", "/api/v1/admin/docs/{id:long}", "site:read"),
            ("GET", "/api/v1/admin/docs/by-slug/{*slug}", "site:read"), ("GET", "/api/v1/admin/docs/categories", "site:read"),
            ("GET", "/api/v1/admin/docs/{parentId:long}/children", "site:read"), ("GET", "/api/v1/admin/docs/{id:long}/translations", "site:read"),
            ("POST", "/api/v1/admin/docs/{id:long}/translations", "site:create"), ("POST", "/api/v1/admin/docs/", "site:create"),
            ("PUT", "/api/v1/admin/docs/{id:long}", "site:update"), ("POST", "/api/v1/admin/docs/{spaceId:long}/sections/{parentId:long}/children", "site:create"),
            ("POST", "/api/v1/admin/docs/{spaceId:long}/sections/{id:long}/move", "site:update"), ("POST", "/api/v1/admin/docs/{spaceId:long}/sections/reorder", "site:update"),
            ("POST", "/api/v1/admin/docs/{id:long}/publish", "site:update"), ("POST", "/api/v1/admin/docs/{id:long}/unpublish", "site:update"),
            ("DELETE", "/api/v1/admin/docs/{id:long}", "site:delete")
        };
        foreach (var item in expected)
        {
            var endpoint = endpoints.Single(x => x.RoutePattern.RawText == item.Route && x.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(item.Method));
            await Assert.That(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(x => x.Policy)).Contains(item.Policy);
        }
    }

    [Test]
    public async Task CreateAndUpdateGuards_RejectBeforeActor()
    {
        var actor = Substitute.For<IAeroDocsActor>();
        await using var app = await CreateAppAsync(actor);
        var client = app.GetTestClient();
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/docs/") { Content = JsonContent.Create(new DocViewModel { Id = 9 }) };
        create.WithTestUser(42);
        using var createResponse = await client.SendAsync(create);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var update = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/docs/10") { Content = JsonContent.Create(new DocViewModel { Id = 11 }) };
        update.WithTestUser(42);
        using var updateResponse = await client.SendAsync(update);
        await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await actor.DidNotReceive().SaveAsync(Arg.Any<DocViewModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_ForeignOrMissing_IsConcealedBeforeSave()
    {
        var actor = Substitute.For<IAeroDocsActor>();
        actor.GetByIdAsync(10, 42, Arg.Any<CancellationToken>()).Returns(new AeroRequestResponse<DocViewModel>(new(), new DocErrorViewModel { Message = "not found" }));
        await using var app = await CreateAppAsync(actor);
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/docs/10") { Content = JsonContent.Create(new DocViewModel { Id = 10 }) };
        request.WithTestUser(42);
        using var response = await app.GetTestClient().SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await actor.DidNotReceive().SaveAsync(Arg.Any<DocViewModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Delete_MapsChildProtectionToConflict_AndMissingToNotFound()
    {
        var actor = Substitute.For<IAeroDocsActor>();
        actor.DeleteDocAsync(10, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<DocViewModel>(
                new(),
                new DocErrorViewModel
                {
                    Message = "A documentation section with child sections cannot be deleted.",
                    Errors = ["A documentation section with child sections cannot be deleted."]
                }));
        actor.DeleteDocAsync(11, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<DocViewModel>(
                new(),
                new DocErrorViewModel { Message = "not found" }));
        await using var app = await CreateAppAsync(actor);

        var protectedDelete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/docs/10");
        protectedDelete.WithTestUser(42);
        using var protectedResponse = await app.GetTestClient().SendAsync(protectedDelete);

        var missingDelete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/docs/11");
        missingDelete.WithTestUser(42);
        using var missingResponse = await app.GetTestClient().SendAsync(missingDelete);

        await Assert.That(protectedResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(missingResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<WebApplication> CreateAppAsync(IAeroDocsActor actor)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Services.AddLogging(); builder.Services.AddTestAuthentication(); builder.Services.AddSingleton(actor);
        var site = Substitute.For<ISiteContext>(); site.SiteId.Returns(42); builder.Services.AddSingleton(site);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapDocsApi(); await app.StartAsync(); return app;
    }
}
