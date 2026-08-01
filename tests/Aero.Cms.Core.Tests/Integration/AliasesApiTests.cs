using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Validators;
using Aero.Cms.Modules.Aliases.Areas.Api.v1;
using Aero.Core.Http;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class AliasesApiTests
{
    [Test]
    public async Task AdminApi_DeclaresExactlyThreeSitePolicyEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapAliasesApi();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
        await Assert.That(endpoints.Count).IsEqualTo(3);
        var expected = new[]
        {
            ("GET", "/api/v1/admin/aliases/", "site:read"),
            ("POST", "/api/v1/admin/aliases/", "site:create"),
            ("DELETE", "/api/v1/admin/aliases/{id:long}", "site:delete")
        };
        foreach (var item in expected)
        {
            var endpoint = endpoints.Single(x => x.RoutePattern.RawText == item.Item2 && x.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(item.Item1));
            await Assert.That(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(x => x.Policy)).Contains(item.Item3);
        }
    }

    [Test]
    public async Task QueryAndBodySiteIds_CannotOverrideSelectedSite()
    {
        var actor = Substitute.For<IAeroAliasActor>();
        actor.GetAllAliasesAsync(42, Arg.Any<CancellationToken>()).Returns([]);
        actor.CreateAliasAsync(Arg.Any<CreateAliasRequest>(), 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 1, SiteId = 42 }, new AliasErrorViewModel()));
        await using var app = await CreateAppAsync(actor);
        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/aliases/?siteId=99"); get.WithTestUser(7);
        using var getResponse = await app.GetTestClient().SendAsync(get);
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await actor.Received(1).GetAllAliasesAsync(42, Arg.Any<CancellationToken>());

        using var post = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/aliases/")
        { Content = JsonContent.Create(new CreateAliasRequest(99, "/old", "/new")) };
        post.WithTestUser(7);
        using var postResponse = await app.GetTestClient().SendAsync(post);
        await Assert.That(postResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await actor.Received(1).CreateAliasAsync(Arg.Is<CreateAliasRequest>(x => x.SiteId == 42), 42, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Delete_ForeignOrMissingIs404BeforeMutation_AndSuccessIs204()
    {
        var actor = Substitute.For<IAeroAliasActor>();
        actor.GetByIdAsync(5, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new(), new AliasErrorViewModel { Message = "not found" }));
        await using var app = await CreateAppAsync(actor);
        using var missing = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/aliases/5"); missing.WithTestUser(7);
        using var missingResponse = await app.GetTestClient().SendAsync(missing);
        await Assert.That(missingResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await actor.DidNotReceive().DeleteAliasAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());

        actor.GetByIdAsync(6, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 6, SiteId = 42 }, new AliasErrorViewModel()));
        actor.DeleteAliasAsync(6, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 6, SiteId = 42 }, new AliasErrorViewModel()));
        using var valid = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/aliases/6"); valid.WithTestUser(7);
        using var validResponse = await app.GetTestClient().SendAsync(valid);
        await Assert.That(validResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        actor.GetByIdAsync(7, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new AliasViewModel { Id = 7, SiteId = 42 }, new AliasErrorViewModel()));
        actor.DeleteAliasAsync(7, 42, Arg.Any<CancellationToken>())
            .Returns(new AeroRequestResponse<AliasViewModel>(new(), new AliasErrorViewModel { Message = "not found" }));
        using var raced = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/aliases/7"); raced.WithTestUser(7);
        using var racedResponse = await app.GetTestClient().SendAsync(raced);
        await Assert.That(racedResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<WebApplication> CreateAppAsync(IAeroAliasActor actor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();
        builder.Services.AddSingleton(actor);
        builder.Services.AddSingleton<IValidator<CreateAliasRequest>>(new CreateAliasRequestValidator());
        builder.Services.AddSingleton<IValidator<DeleteAliasRequest>>(new DeleteAliasRequestValidator());
        var site = Substitute.For<ISiteContext>(); site.SiteId.Returns(42); builder.Services.AddSingleton(site);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapAliasesApi(); await app.StartAsync(); return app;
    }
}
