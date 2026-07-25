using System.Net;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Web.Areas.Api.V1;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Web.Tests;

public sealed class PublicCmsQueryApiTests
{
    [Test]
    public async Task Pages_endpoint_returns_encoded_htmx_and_private_culture_safe_headers()
    {
        var service = Substitute.For<IPublicCmsQueryService>();
        var page = new PublicQueryPage<PublicPageQueryItem>(
            [
                new PublicPageQueryItem(
                    "9007199254740993",
                    "<script>alert('x')</script>",
                    "page",
                    "/page\" onclick=\"bad",
                    "<b>summary</b>",
                    "en-US",
                    null)
            ],
            1,
            0,
            1);
        service.QueryPagesAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<PublicQueryPage<PublicPageQueryItem>>>(
                new Result<PublicQueryPage<PublicPageQueryItem>>.Ok(page)));

        await using var app = await CreateAppAsync(service);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/query/pages?take=1");
        request.Headers.Add("HX-Request", "true");

        using var response = await app.GetTestClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        response.Headers.CacheControl?.Private.Should().BeTrue();
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        response.Headers.Vary.Should().Contain("HX-Request");
        body.Should().Contain("9007199254740993");
        body.Should().Contain("&lt;script&gt;");
        body.Should().NotContain("<script>");
        body.Should().NotContain("onclick=\"bad");
    }

    [Test]
    public async Task Pages_endpoint_returns_snowflake_ids_as_json_strings()
    {
        var service = Substitute.For<IPublicCmsQueryService>();
        var page = new PublicQueryPage<PublicPageQueryItem>(
            [
                new PublicPageQueryItem(
                    "9007199254740993",
                    "Page",
                    "page",
                    "/page",
                    null,
                    "en-US",
                    null)
            ],
            1,
            0,
            1);
        service.QueryPagesAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<PublicQueryPage<PublicPageQueryItem>>>(
                new Result<PublicQueryPage<PublicPageQueryItem>>.Ok(page)));

        await using var app = await CreateAppAsync(service);
        using var response = await app.GetTestClient()
            .GetAsync("/api/v1/query/pages?take=1");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        body.Should().Contain("\"id\":\"9007199254740993\"");
    }

    [Test]
    public async Task Pages_endpoint_does_not_publicly_cache_validation_failures()
    {
        var service = Substitute.For<IPublicCmsQueryService>();
        service.QueryPagesAsync(0, 99, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<PublicQueryPage<PublicPageQueryItem>>>(
                new Result<PublicQueryPage<PublicPageQueryItem>>.Failure(
                    AeroError.ValidationError(["take is out of range."]))));

        await using var app = await CreateAppAsync(service);
        using var response = await app.GetTestClient()
            .GetAsync("/api/v1/query/pages?take=99");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.CacheControl?.Private.Should().BeTrue();
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        response.Headers.CacheControl!.Public.Should().BeFalse();
    }

    [Test]
    public async Task Query_service_rejects_unbounded_page_requests_before_persistence()
    {
        var service = new PublicCmsQueryService(
            Substitute.For<IDocumentSession>(),
            Substitute.For<ISiteContext>(),
            Substitute.For<IContentTypeService>(),
            Substitute.For<IContentHierarchyQueryService>(),
            Substitute.For<ILogger<PublicCmsQueryService>>());

        var result = await service.QueryPagesAsync(
            0,
            PublicCmsQueryService.MaximumTake + 1);

        result.Should().BeOfType<
            Result<PublicQueryPage<PublicPageQueryItem>>.Failure>();
    }

    private static async Task<WebApplication> CreateAppAsync(
        IPublicCmsQueryService service)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(service);

        var app = builder.Build();
        app.MapPublicCmsQueryApi();
        await app.StartAsync();
        return app;
    }
}
