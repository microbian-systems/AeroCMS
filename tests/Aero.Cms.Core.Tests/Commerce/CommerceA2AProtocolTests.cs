using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.A2A.Api;
using Aero.Cms.Modules.Commerce.A2A.Models;
using Aero.Cms.Modules.Commerce.A2A.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Core.Tests.Integration;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceA2AProtocolTests
{
    private const long TenantId = 42;
    private const long StoreSiteId = 10;
    private const long OtherSiteId = 11;
    private const string StoreHost = "store.example.test";
    private const string OtherHost = "other.example.test";
    private const string LocalHost = "localhost";

    [Test]
    public async Task Disabled_and_unresolved_hosts_conceal_the_agent_card_and_message_endpoint()
    {
        var settings = CreateSettings(enabledSites: []);
        var products = Substitute.For<IProductService>();
        await using var app = await CreateAppAsync(settings, products);

        foreach (var host in new[] { StoreHost, "unknown.example.test" })
        {
            using var card = await SendAsync(app, HttpMethod.Get, "/.well-known/agent-card.json", host);
            using var message = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", host, SendMessage("search_products", new { }));

            card.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            card.Headers.CacheControl!.NoStore.ShouldBeTrue();
            message.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await products.DidNotReceiveWithAnyArgs().SearchPublishedAsync(default, default, default!, default, default, default, default, default, default);
        await products.DidNotReceiveWithAnyArgs().GetPublishedListingBySlugAsync(default, default, default!, default!, default);
    }

    [Test]
    public async Task Invalid_agent_card_host_is_concealed_without_caching_when_a2a_is_enabled()
    {
        await using var app = await CreateAppAsync(CreateSettings(StoreSiteId), Substitute.For<IProductService>());

        using var response = await SendAsync(app, HttpMethod.Get, "/.well-known/agent-card.json", LocalHost);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task Enabled_card_is_host_scoped_and_uses_source_generated_camel_case_serialization()
    {
        A2ACommerceJsonContext.Default.A2AAgentCard.ShouldNotBeNull();

        var settings = CreateSettings(StoreSiteId);
        await using var app = await CreateAppAsync(settings, Substitute.For<IProductService>());

        using var response = await SendAsync(app, HttpMethod.Get, "/.well-known/agent-card.json", StoreHost);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();

        using var card = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        card.RootElement.GetProperty("supportedInterfaces")[0].GetProperty("url").GetString()
            .ShouldBe("https://store.example.test/a2a/commerce");
        card.RootElement.GetProperty("supportedInterfaces")[0].GetProperty("protocolVersion").GetString().ShouldBe("1.0");
        card.RootElement.GetProperty("skills").EnumerateArray().Select(x => x.GetProperty("id").GetString())
            .ShouldBe(["search_products", "get_product"]);
        card.RootElement.TryGetProperty("SupportedInterfaces", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Host_scope_cannot_be_changed_by_a_selected_site_cookie_or_cross_site_host()
    {
        var settings = CreateSettings(StoreSiteId);
        var products = Substitute.For<IProductService>();
        products.SearchPublishedAsync(TenantId, StoreSiteId, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([], 0)));
        await using var app = await CreateAppAsync(settings, products);

        using var sameHost = await SendAsync(
            app,
            HttpMethod.Post,
            "/a2a/commerce",
            StoreHost,
            SendMessage("search_products", new { }),
            cookie: $"AeroCms.SiteId={OtherSiteId}");
        using var otherHost = await SendAsync(app, HttpMethod.Get, "/.well-known/agent-card.json", OtherHost);

        sameHost.StatusCode.ShouldBe(HttpStatusCode.OK);
        otherHost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await products.Received(1).SearchPublishedAsync(TenantId, StoreSiteId, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>());
        await products.DidNotReceive().SearchPublishedAsync(TenantId, OtherSiteId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Send_message_exposes_only_published_active_current_culture_listings_for_both_skills()
    {
        await using var harness = await CreateHarnessAsync();
        var culture = CultureInfo.CurrentUICulture.Name;
        harness.Session.Store(Product(100, "Green tea", active: true));
        harness.Session.Store(Product(101, "Inactive tea", active: false));
        harness.Session.Store(Product(102, "Unpublished tea", active: true));
        harness.Session.Store(Listing(100, "green-tea", "Green tea", culture));
        harness.Session.Store(Listing(101, "inactive-tea", "Inactive tea", culture));
        harness.Session.Store(Listing(102, "unpublished-tea", "Unpublished tea", culture, published: false));
        harness.Session.Store(Listing(100, "the-vert", "The vert", "fr-FR", id: 1_001));
        await harness.Session.SaveChangesAsync();

        var products = new ProductService(harness.Session, new ProductValidator(), new ProductListingValidator());
        await using var app = await CreateAppAsync(CreateSettings(StoreSiteId), products);

        using var search = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { query = "tea" }), version: "1.0");
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        var searchData = ResponseData(searchJson);
        search.StatusCode.ShouldBe(HttpStatusCode.OK);
        searchData.GetProperty("products").GetArrayLength().ShouldBe(1);
        searchData.GetProperty("products")[0].GetProperty("slug").GetString().ShouldBe("green-tea");
        searchData.GetProperty("totalCount").GetInt64().ShouldBe(1);

        using var get = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("get_product", new { slug = "green-tea" }), version: "1.0");
        using var getJson = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        ResponseData(getJson).GetProperty("product").GetProperty("slug").GetString().ShouldBe("green-tea");

        foreach (var excludedSlug in new[] { "inactive-tea", "unpublished-tea" })
        {
            using var excluded = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("get_product", new { slug = excludedSlug }));
            using var excludedJson = JsonDocument.Parse(await excluded.Content.ReadAsStringAsync());
            excluded.StatusCode.ShouldBe(HttpStatusCode.OK);
            ResponseData(excludedJson).GetProperty("product").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Test]
    public async Task Successful_protocol_output_contains_only_the_public_catalog_projection()
    {
        var settings = CreateSettings(StoreSiteId);
        var products = Substitute.For<IProductService>();
        products.SearchPublishedAsync(TenantId, StoreSiteId, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([Listing(100, "safe-product", "Safe product", CultureInfo.CurrentUICulture.Name)], 1)));
        await using var app = await CreateAppAsync(settings, products);

        using var response = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { }));
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var product = ResponseData(document).GetProperty("products")[0];

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        product.EnumerateObject().Select(x => x.Name).ShouldBe([
            "id", "slug", "name", "shortDescription", "description", "category", "imageUrl",
            "price", "compareAtPrice", "currency", "isFeatured"
        ], ignoreOrder: true);
        json.ShouldNotContain("tenantId");
        json.ShouldNotContain("siteId");
        json.ShouldNotContain("stockQuantity");
        json.ShouldNotContain("customer");
        json.ShouldNotContain("cart");
        json.ShouldNotContain("basket");
        json.ShouldNotContain("isSubscription");
        json.ShouldNotContain("subscriptionIntervalDays");
        document.RootElement.GetProperty("jsonrpc").GetString().ShouldBe("2.0");
    }

    [Test]
    public async Task Bounds_and_oversize_requests_are_rejected_before_catalog_access()
    {
        var settings = CreateSettings(StoreSiteId);
        var products = Substitute.For<IProductService>();
        await using var app = await CreateAppAsync(settings, products);

        using var tooLongQuery = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { query = new string('q', 201) }));
        using var invalidTake = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { take = 101 }));
        using var oversized = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, new string('x', 16 * 1024 + 1));

        tooLongQuery.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadRpcErrorAsync(tooLongQuery)).Code.ShouldBe(-32602);
        invalidTake.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadRpcErrorAsync(invalidTake)).Code.ShouldBe(-32602);
        oversized.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        await products.DidNotReceiveWithAnyArgs().SearchPublishedAsync(default, default, default!, default, default, default, default, default, default);
    }

    [Test]
    public async Task Version_method_skill_and_task_continuations_are_rejected_with_standard_json_rpc_errors()
    {
        await using var app = await CreateAppAsync(CreateSettings(StoreSiteId), Substitute.For<IProductService>());

        using var unsupportedVersion = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { }), version: "9.9");
        using var implicitLegacyVersion = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { }), version: null);
        using var unsupportedMethod = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, """{"jsonrpc":"2.0","id":1,"method":"GetTask","params":{}}""");
        using var unsupportedSkill = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("delete_everything", new { }));
        using var continuation = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, SendMessage("search_products", new { }, taskId: "continued-task"));

        (await ReadRpcErrorAsync(unsupportedVersion)).Code.ShouldBe(-32009);
        (await ReadRpcErrorAsync(implicitLegacyVersion)).Code.ShouldBe(-32009);
        (await ReadRpcErrorAsync(unsupportedMethod)).Code.ShouldBe(-32601);
        (await ReadRpcErrorAsync(unsupportedSkill)).Code.ShouldBe(-32602);
        (await ReadRpcErrorAsync(continuation)).Code.ShouldBe(-32004);
    }

    [Test]
    public async Task Complete_v1_send_message_accepts_standard_optional_fields_and_preserves_a_context_only_direct_message()
    {
        var products = Substitute.For<IProductService>();
        products.SearchPublishedAsync(TenantId, StoreSiteId, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<(IReadOnlyList<ProductListingDocument>, long), AeroError>(([], 0)));
        await using var app = await CreateAppAsync(CreateSettings(StoreSiteId), products);

        using var response = await SendAsync(
            app,
            HttpMethod.Post,
            "/a2a/commerce",
            StoreHost,
            CompleteV1SendMessage(),
            extensions: "https://example.test/a2a/extensions/client-context/v1");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        document.RootElement.GetProperty("result").GetProperty("message").GetProperty("contextId").GetString()
            .ShouldBe("catalog-browse-1");
        await products.Received(1).SearchPublishedAsync(TenantId, StoreSiteId, Arg.Any<string>(), null, null, 0, 20, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Unsupported_part_media_type_uses_the_standard_content_type_not_supported_error()
    {
        await using var app = await CreateAppAsync(CreateSettings(StoreSiteId), Substitute.For<IProductService>());

        using var response = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost,
            """{"jsonrpc":"2.0","id":1,"method":"SendMessage","params":{"message":{"messageId":"message-1","role":"ROLE_USER","parts":[{"mediaType":"text/plain","text":"search products"}]}}}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadRpcErrorAsync(response)).Code.ShouldBe(-32005);
    }

    [Test]
    public async Task Malformed_and_unknown_field_payloads_return_safe_json_rpc_errors()
    {
        await using var app = await CreateAppAsync(CreateSettings(StoreSiteId), Substitute.For<IProductService>());

        using var malformed = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost, "{ definitely-not-json");
        using var unknownEnvelopeField = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost,
            """{"jsonrpc":"2.0","id":1,"method":"SendMessage","params":{},"unexpected":true}""");
        using var unknownParamsField = await SendAsync(app, HttpMethod.Post, "/a2a/commerce", StoreHost,
            """{"jsonrpc":"2.0","id":1,"method":"SendMessage","params":{"message":null,"unexpected":true}}""");

        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadRpcErrorAsync(malformed)).Code.ShouldBe(-32700);
        unknownEnvelopeField.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadRpcErrorAsync(unknownEnvelopeField)).Code.ShouldBe(-32700);
        unknownParamsField.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadRpcErrorAsync(unknownParamsField)).Code.ShouldBe(-32602);
    }

    private static async Task<WebApplication> CreateAppAsync(IA2ASettingsService settings, IProductService products)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(products);
        builder.Services.AddSingleton(new HostSiteMap(new Dictionary<string, SiteScope>(StringComparer.OrdinalIgnoreCase)
        {
            [StoreHost] = new(TenantId, StoreSiteId),
            [OtherHost] = new(TenantId, OtherSiteId),
            [LocalHost] = new(TenantId, StoreSiteId)
        }));
        builder.Services.AddScoped<ISiteContext, HostSiteContext>();

        var app = builder.Build();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            statusCodeContext.HttpContext.Response.Headers.CacheControl = "public, max-age=60";
            await statusCodeContext.HttpContext.Response.WriteAsync("fallback status page");
        });
        app.MapA2ACommerceApi();
        await app.StartAsync();
        return app;
    }

    private static IA2ASettingsService CreateSettings(params long[] enabledSites)
    {
        var settings = Substitute.For<IA2ASettingsService>();
        settings.GetAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<Result<A2ASettingsResponse, AeroError>>(
                Prelude.Ok<A2ASettingsResponse, AeroError>(new A2ASettingsResponse(
                    call.ArgAt<long>(0) == TenantId && enabledSites.Contains(call.ArgAt<long>(1))))));
        return settings;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        WebApplication app,
        HttpMethod method,
        string path,
        string host,
        string? body = null,
        string? version = "1.0",
        string? cookie = null,
        string? extensions = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Host = host;
        if (version is not null)
            request.Headers.Add("A2A-Version", version);
        if (extensions is not null)
            request.Headers.Add("A2A-Extensions", extensions);
        if (cookie is not null)
            request.Headers.Add("Cookie", cookie);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await app.GetTestClient().SendAsync(request);
    }

    private static string SendMessage(string skillId, object input, string? contextId = null, string? taskId = null)
        => JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "SendMessage",
            @params = new
            {
                message = new
                {
                    messageId = "message-1",
                    contextId,
                    taskId,
                    role = "ROLE_USER",
                    parts = new[] { new { mediaType = "application/json", data = new { skillId, input } } }
                }
            }
        });

    private static string CompleteV1SendMessage()
        => """
           {
             "jsonrpc":"2.0",
             "id":"request-1",
             "method":"SendMessage",
             "params":{
               "tenant":"ignored-by-host-scoped-catalog",
               "metadata":{"client":"sample","trace":{"id":"trace-1"}},
               "configuration":{
                 "acceptedOutputModes":["application/json"],
                 "historyLength":0,
                 "returnImmediately":true,
                 "taskPushNotificationConfig":{
                   "id":"",
                   "taskId":"",
                   "tenant":"",
                   "token":"not-a-secret",
                   "url":"https://client.example.test/a2a/updates",
                   "authentication":{"scheme":"Bearer","credentials":"not-a-secret"}
                 }
               },
               "message":{
                 "messageId":"message-1",
                 "contextId":"catalog-browse-1",
                 "role":"ROLE_USER",
                 "extensions":["https://example.test/a2a/extensions/client-context/v1"],
                 "metadata":{"locale":"en-US"},
                 "referenceTaskIds":["related-task-1"],
                 "parts":[{
                   "mediaType":"application/json; charset=utf-8",
                   "data":{"skillId":"search_products","input":{}},
                   "metadata":{"source":"test"}
                 }]
               }
             }
           }
           """;

    private static JsonElement ResponseData(JsonDocument response)
        => response.RootElement.GetProperty("result").GetProperty("message").GetProperty("parts")[0].GetProperty("data");

    private static async Task<A2AJsonRpcError> ReadRpcErrorAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        return new A2AJsonRpcError(error.GetProperty("code").GetInt32(), error.GetProperty("message").GetString()!);
    }

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }

    private static ProductDocument Product(long id, string name, bool active) => new()
    {
        Id = id,
        TenantId = TenantId,
        Name = name,
        Sku = $"SKU-{id}",
        IsActive = active
    };

    private static ProductListingDocument Listing(long productId, string slug, string name, string culture, bool published = true, long? id = null) => new()
    {
        Id = id ?? productId * 10,
        TenantId = TenantId,
        SiteId = StoreSiteId,
        ProductId = productId,
        Culture = culture,
        Slug = slug,
        Name = name,
        ShortDescription = "Public description",
        Description = "Public product description",
        Category = "Tea",
        ImageUrl = "https://images.example.test/tea.jpg",
        Price = 12.50m,
        Currency = "USD",
        IsPublished = published
    };

    private sealed record SiteScope(long TenantId, long SiteId);

    private sealed class HostSiteMap(IReadOnlyDictionary<string, SiteScope> scopes)
    {
        public bool TryGet(string host, out SiteScope scope) => scopes.TryGetValue(host, out scope!);
    }

    private sealed class HostSiteContext(IHttpContextAccessor accessor, HostSiteMap sites) : ISiteContext
    {
        private SiteScope? Scope => sites.TryGet(accessor.HttpContext?.Request.Host.Host ?? string.Empty, out var scope) ? scope : null;
        public long TenantId => Scope?.TenantId ?? 0;
        public long SiteId => Scope?.SiteId ?? 0;
    }
}
