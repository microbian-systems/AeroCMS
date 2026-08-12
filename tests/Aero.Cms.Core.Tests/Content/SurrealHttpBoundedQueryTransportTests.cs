using System.Net;
using System.Text;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class SurrealHttpBoundedQueryTransportTests
{
    [Test]
    public async Task Dedicated_http_transport_binds_parameters_and_materializes_only_a_bounded_response()
    {
        HttpRequestMessage? observed = null;
        string? observedBody = null;
        using var client = new HttpClient(new Handler(async request =>
        {
            observed = request;
            observedBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, "[{\"time\":\"1ms\",\"status\":\"OK\",\"result\":[{\"externalId\":\"entry-42\",\"displayName\":\"Sample entry\"}]}]");
        }));
        using var transport = new SurrealHttpBoundedQueryTransport(Options("https://db.example.test/rpc"), client);

        var result = await transport.ExecuteBoundedAsync(Request(new Dictionary<string, object?>
        {
            ["$tenantId"] = 11L,
            ["$siteId"] = 22L,
            ["$search"] = "sample entry"
        }));

        transport.EnforcesLimitsBeforeMaterialization.ShouldBeTrue();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].Keys.ShouldBe(["externalId", "displayName"], ignoreOrder: true);
        observed.ShouldNotBeNull();
        observed!.RequestUri!.AbsolutePath.ShouldBe("/sql");
        observed.RequestUri.Query.ShouldContain("tenantId=11");
        observed.RequestUri.Query.ShouldContain("siteId=22");
        observed.RequestUri.Query.ShouldContain("search=sample%20entry");
        observed.Headers.GetValues("surreal-ns").Single().ShouldBe("aero");
        observed.Headers.GetValues("surreal-db").Single().ShouldBe("content_test");
        observed.Headers.Authorization!.Scheme.ShouldBe("Basic");
        observedBody.ShouldBe(RequestStatement);
        observedBody.ShouldNotContain("sample entry");
    }

    [Test]
    public async Task Response_is_rejected_before_json_materialization_when_wire_bytes_exceed_the_limit()
    {
        var oversized = Encoding.UTF8.GetBytes(new string('x', 129));
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(oversized)
        })));
        using var transport = new SurrealHttpBoundedQueryTransport(Options("https://db.example.test/sql"), client);
        var request = Request(new Dictionary<string, object?>()) with
        {
            Limits = new ContentViewExecutionLimits(10, 10, 128, 8)
        };

        await Should.ThrowAsync<InvalidOperationException>(() => transport.ExecuteBoundedAsync(request));
    }

    [Test]
    public async Task Requested_page_is_truncated_without_fabricating_a_total()
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(Json(HttpStatusCode.OK,
            "[{\"status\":\"OK\",\"result\":[{\"externalId\":\"one\"},{\"externalId\":\"two\"}]}]"))));
        using var transport = new SurrealHttpBoundedQueryTransport(Options("https://db.example.test/sql"), client);
        var request = Request(new Dictionary<string, object?>()) with { Take = 1 };

        var result = await transport.ExecuteBoundedAsync(request);

        result.Rows.Count.ShouldBe(1);
        result.IsTruncated.ShouldBeTrue();
    }

    [Test]
    public void Remote_cleartext_endpoint_never_activates_the_transport()
    {
        using var transport = new SurrealHttpBoundedQueryTransport(
            Options("http://db.example.test:8000/rpc"),
            new HttpClient(new Handler(_ => throw new InvalidOperationException("No request expected."))));

        transport.EnforcesLimitsBeforeMaterialization.ShouldBeFalse();
        ((IAdminReadOnlyContentViewExecutor)transport).IsReadOnlyGuaranteed.ShouldBeFalse();
    }

    [Test]
    public async Task Non_scalar_parameters_fail_closed_before_the_request_is_sent()
    {
        var sent = false;
        using var client = new HttpClient(new Handler(_ =>
        {
            sent = true;
            return Task.FromResult(Json(HttpStatusCode.OK, "[]"));
        }));
        using var transport = new SurrealHttpBoundedQueryTransport(Options("https://db.example.test/sql"), client);

        await Should.ThrowAsync<InvalidOperationException>(() => transport.ExecuteBoundedAsync(
            Request(new Dictionary<string, object?> { ["$unsafe"] = new[] { "a", "b" } })));
        sent.ShouldBeFalse();
    }

    private const string RequestStatement = "SELECT external_id, display_name FROM registered_catalog_read WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 10";

    private static SableReadOnlyContentViewOptions Options(string endpoint) => new()
    {
        Endpoint = endpoint,
        Namespace = "aero",
        Database = "content_test",
        Username = "readonly",
        Password = "test-only"
    };

    private static ContentViewExecutionRequest Request(IReadOnlyDictionary<string, object?> parameters)
    {
        var scope = new ContentViewScope(11, 22);
        var view = new ContentSurrealViewRevision(1, scope, "catalog-entry", "catalog-entry", "fingerprint",
            RequestStatement, "externalId", "displayName", 1, ContentViewPublicationState.Published,
            DateTimeOffset.UtcNow);
        return new ContentViewExecutionRequest(view, scope, 10, parameters,
            new ContentViewExecutionLimits(10, 10, 16_384, 8),
            new ContentViewSourceDefinition("catalog-entry", "registered_catalog_read"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request);
    }
}
