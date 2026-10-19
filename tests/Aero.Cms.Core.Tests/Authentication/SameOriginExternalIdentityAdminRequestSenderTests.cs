using System.Net;
using System.Text;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Web.Client;
using Aero.Cms.Web.Client.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class SameOriginExternalIdentityAdminRequestSenderTests
{
    private static readonly Uri HostBaseAddress = new("https://manager.test/");
    private static readonly Uri ConfiguredApiBaseAddress = new("https://configured-api.test/");

    [Test]
    public async Task AddAeroCmsClient_registers_the_same_origin_sender_without_using_the_configured_api_origin()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiSettings:BaseUrl"] = ConfiguredApiBaseAddress.ToString()
            })
            .Build();

        services.AddAeroCmsClient(configuration, HostBaseAddress.ToString());
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IExternalIdentityAdminRequestSender>();

        await Assert.That(sender).IsTypeOf<SameOriginExternalIdentityAdminRequestSender>();
    }

    [Test]
    public async Task Exact_authority_get_is_same_origin_credentialed_and_never_performs_an_antiforgery_handshake()
    {
        using var cancellation = BoundedCancellation();
        using var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{}"));
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/admin/external-identity/authority");

        using var response = await sender.SendAsync(request, cancellation.Token);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(handler.Requests.Count).IsEqualTo(1);
        await Assert.That(handler.Requests[0].Path).IsEqualTo("/api/v1/admin/external-identity/authority");
        await Assert.That(handler.Requests[0].CredentialsIncluded).IsTrue();
        await Assert.That(handler.Requests[0].RedirectFailsClosed).IsTrue();
        await Assert.That(handler.Requests[0].Headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Each_unsafe_request_gets_a_fresh_same_origin_token_and_sends_only_its_negotiated_header()
    {
        using var cancellation = BoundedCancellation();
        using var handler = new RecordingHandler(request => request.Path switch
        {
            "/api/v1/admin/external-identity/antiforgery" => Json(HttpStatusCode.OK,
                handlerToken(request.Ordinal)),
            "/api/v1/admin/external-identity/authority" => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

        using var first = await sender.SendAsync(AuthorityRequest(), cancellation.Token);
        using var second = await sender.SendAsync(AuthorityRequest(), cancellation.Token);

        await Assert.That(handler.Requests.Count).IsEqualTo(4);
        await Assert.That(handler.Requests.Select(request => request.Path)
            .SequenceEqual([
                "/api/v1/admin/external-identity/antiforgery",
                "/api/v1/admin/external-identity/authority",
                "/api/v1/admin/external-identity/antiforgery",
                "/api/v1/admin/external-identity/authority"])).IsTrue();
        var mutations = handler.Requests.Where(request => request.Method == HttpMethod.Put).ToArray();
        await Assert.That(mutations.Length).IsEqualTo(2);
        await Assert.That(mutations[0].Headers).ContainsKey("X-CSRF-TOKEN");
        await Assert.That(mutations[0].Headers["X-CSRF-TOKEN"]).IsEqualTo("token-1");
        await Assert.That(mutations[1].Headers["X-CSRF-TOKEN"]).IsEqualTo("token-2");
        await Assert.That(handler.Requests.All(request => request.CredentialsIncluded)).IsTrue();
        await Assert.That(handler.Requests.All(request => request.RedirectFailsClosed)).IsTrue();
        await Assert.That(mutations.All(request => request.Headers.Count == 1)).IsTrue();
        await Assert.That(handler.Requests.All(request => HostBaseAddress.IsBaseOf(request.Uri))).IsTrue();
    }

    [Test]
    public async Task Invalid_method_uri_escape_or_foreign_route_is_rejected_before_any_handshake_or_mutation()
    {
        using var cancellation = BoundedCancellation();
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);
        var invalid = new[]
        {
            new HttpRequestMessage(HttpMethod.Get, "api/v1/admin/external-identity/invitations"),
            new HttpRequestMessage(HttpMethod.Put, "https://foreign.test/api/v1/admin/external-identity/authority"),
            new HttpRequestMessage(HttpMethod.Put, "//foreign.test/api/v1/admin/external-identity/authority"),
            new HttpRequestMessage(HttpMethod.Put, "../api/v1/admin/external-identity/authority"),
            new HttpRequestMessage(HttpMethod.Put, "api/v1/admin/external-members/42"),
            new HttpRequestMessage(HttpMethod.Post, "api/v1/admin/external-identity/authority")
        };

        foreach (var request in invalid)
        {
            await Assert.That(async () => await sender.SendAsync(request, cancellation.Token))
                .Throws<InvalidOperationException>();
            request.Dispose();
        }

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task Missing_malformed_or_unsafe_handshake_material_prevents_the_mutation()
    {
        using var cancellation = BoundedCancellation();
        var invalidResponses = new[]
        {
            "{}",
            "{\"headerName\":\"Cookie\",\"requestToken\":\"token\"}",
            "{\"headerName\":\"X-CSRF-TOKEN\",\"requestToken\":\"token\\r\\nother\"}",
            $"{{\"headerName\":\"X-CSRF-TOKEN\",\"requestToken\":\"{new string('a', 4097)}\"}}"
        };

        foreach (var body in invalidResponses)
        {
            using var handler = new RecordingHandler(request => request.Path == "/api/v1/admin/external-identity/antiforgery"
                ? Json(HttpStatusCode.OK, body)
                : new HttpResponseMessage(HttpStatusCode.NoContent));
            using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
            var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

            await Assert.That(async () => await sender.SendAsync(AuthorityRequest(), cancellation.Token))
                .Throws<InvalidOperationException>();
            await Assert.That(handler.Requests.Count).IsEqualTo(1);
            await Assert.That(handler.Requests[0].Path).IsEqualTo("/api/v1/admin/external-identity/antiforgery");
        }
    }

    [Test]
    public async Task Handshake_header_that_cannot_be_attached_is_rejected_without_an_unsafe_mutation()
    {
        using var cancellation = BoundedCancellation();
        foreach (var headerName in new[] { "Content-Type", "Set-Cookie" })
        {
            using var handler = new RecordingHandler(request => request.Path == "/api/v1/admin/external-identity/antiforgery"
                ? Json(HttpStatusCode.OK, $"{{\"headerName\":\"{headerName}\",\"requestToken\":\"token\"}}")
                : new HttpResponseMessage(HttpStatusCode.NoContent));
            using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
            var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

            await Assert.That(async () => await sender.SendAsync(AuthorityRequest(), cancellation.Token))
                .Throws<InvalidOperationException>();
            await Assert.That(handler.Requests.All(request => request.Method != HttpMethod.Put)).IsTrue();
        }
    }

    [Test]
    public async Task Missing_response_request_identity_or_uri_fails_closed()
    {
        using var cancellation = BoundedCancellation();
        foreach (var mode in new[] { ResponseIdentityMode.MissingRequestMessage, ResponseIdentityMode.MissingRequestUri })
        {
            using var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{}"), mode);
            using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
            var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/admin/external-identity/authority");

            await Assert.That(async () => await sender.SendAsync(request, cancellation.Token))
                .Throws<InvalidOperationException>();
            await Assert.That(handler.Requests.Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Caller_supplied_legacy_antiforgery_header_is_rejected_and_cannot_ride_with_the_fresh_header()
    {
        using var cancellation = BoundedCancellation();
        using var handler = new RecordingHandler(request => request.Path == "/api/v1/admin/external-identity/antiforgery"
            ? Json(HttpStatusCode.OK, handlerToken(request.Ordinal))
            : new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);
        using var request = AuthorityRequest();
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", "legacy-token");

        await Assert.That(async () => await sender.SendAsync(request, cancellation.Token))
            .Throws<InvalidOperationException>();
        await Assert.That(handler.Requests.All(captured => captured.Method != HttpMethod.Put)).IsTrue();
    }

    [Test]
    public async Task Parallel_mutations_are_serialized_from_token_acquisition_through_response_headers()
    {
        using var cancellation = BoundedCancellation();
        var firstMutationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async request =>
        {
            if (request.Path == "/api/v1/admin/external-identity/antiforgery")
                return Json(HttpStatusCode.OK, handlerToken(request.Ordinal));
            if (request.Path == "/api/v1/admin/external-identity/authority" && request.Ordinal == 2)
            {
                firstMutationStarted.TrySetResult();
                await allowFirstMutation.Task;
            }
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

        using var firstRequest = AuthorityRequest();
        using var secondRequest = AuthorityRequest();
        var first = sender.SendAsync(firstRequest, cancellation.Token);
        await firstMutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token);
        var second = sender.SendAsync(secondRequest, cancellation.Token);
        await Task.Yield();
        await Assert.That(handler.Requests.Select(request => request.Path).SequenceEqual([
            "/api/v1/admin/external-identity/antiforgery",
            "/api/v1/admin/external-identity/authority"])).IsTrue();

        allowFirstMutation.TrySetResult();
        using var firstResponse = await first;
        using var secondResponse = await second;
        await Assert.That(handler.Requests.Select(request => request.Path).SequenceEqual([
            "/api/v1/admin/external-identity/antiforgery",
            "/api/v1/admin/external-identity/authority",
            "/api/v1/admin/external-identity/antiforgery",
            "/api/v1/admin/external-identity/authority"])).IsTrue();
    }

    [Test]
    public async Task Original_cancellation_token_prevents_dispatch_before_wait_during_wait_and_after_handshake()
    {
        using var preCanceled = BoundedCancellation();
        preCanceled.Cancel();
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

        var preCanceledException = await CaptureCancellationAsync(() => sender.SendAsync(AuthorityRequest(), preCanceled.Token));
        await Assert.That(preCanceledException.CancellationToken).IsEqualTo(preCanceled.Token);
        await Assert.That(handler.Requests).IsEmpty();

        using var afterHandshake = BoundedCancellation();
        handler.ReplaceResponse(request =>
        {
            if (request.Path == "/api/v1/admin/external-identity/antiforgery")
            {
                afterHandshake.Cancel();
                return Json(HttpStatusCode.OK, handlerToken(request.Ordinal));
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var afterHandshakeException = await CaptureCancellationAsync(() => sender.SendAsync(AuthorityRequest(), afterHandshake.Token));
        await Assert.That(afterHandshakeException.CancellationToken).IsEqualTo(afterHandshake.Token);
        await Assert.That(handler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Waiting_cancellation_does_not_mutate_and_gate_releases_for_a_later_request()
    {
        using var firstCancellation = BoundedCancellation();
        using var waitingCancellation = BoundedCancellation();
        using var finalCancellation = BoundedCancellation();
        var firstMutationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async request =>
        {
            if (request.Path == "/api/v1/admin/external-identity/antiforgery")
                return Json(HttpStatusCode.OK, handlerToken(request.Ordinal));
            if (request.Ordinal == 2)
            {
                firstMutationStarted.TrySetResult();
                await allowFirstMutation.Task;
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

        var first = sender.SendAsync(AuthorityRequest(), firstCancellation.Token);
        await firstMutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), firstCancellation.Token);
        var waiting = sender.SendAsync(AuthorityRequest(), waitingCancellation.Token);
        waitingCancellation.Cancel();
        await Assert.That(async () => await waiting).Throws<OperationCanceledException>();
        await Assert.That(handler.Requests.Count).IsEqualTo(2);

        allowFirstMutation.TrySetResult();
        using var firstResponse = await first;
        using var finalResponse = await sender.SendAsync(AuthorityRequest(), finalCancellation.Token);
        await Assert.That(handler.Requests.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Unsafe_server_error_is_returned_once_without_retrying_the_mutation()
    {
        using var cancellation = BoundedCancellation();
        using var handler = new RecordingHandler(request => request.Path == "/api/v1/admin/external-identity/antiforgery"
            ? Json(HttpStatusCode.OK, handlerToken(request.Ordinal))
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler) { BaseAddress = ConfiguredApiBaseAddress };
        var sender = new SameOriginExternalIdentityAdminRequestSender(client, HostBaseAddress);

        using var response = await sender.SendAsync(AuthorityRequest(), cancellation.Token);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert.That(handler.Requests.Count).IsEqualTo(2);
        await Assert.That(handler.Requests.Count(request => request.Method == HttpMethod.Put)).IsEqualTo(1);
    }

    private static HttpRequestMessage AuthorityRequest() => new(HttpMethod.Put,
        "api/v1/admin/external-identity/authority")
    {
        Content = new StringContent("{}", Encoding.UTF8, "application/json")
    };

    private static CancellationTokenSource BoundedCancellation() => new(TimeSpan.FromSeconds(5));

    private static async Task<OperationCanceledException> CaptureCancellationAsync(Func<Task> action)
    {
        try
        {
            await action();
            throw new InvalidOperationException("Expected cancellation.");
        }
        catch (OperationCanceledException exception)
        {
            return exception;
        }
    }

    private static string handlerToken(int ordinal) => $"{{\"headerName\":\"X-CSRF-TOKEN\",\"requestToken\":\"token-{(ordinal + 1) / 2}\"}}";

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private Func<CapturedRequest, Task<HttpResponseMessage>> response;

        private readonly ResponseIdentityMode identityMode;

        public RecordingHandler(
            Func<CapturedRequest, HttpResponseMessage> response,
            ResponseIdentityMode identityMode = ResponseIdentityMode.Request)
            : this(request => Task.FromResult(response(request)), identityMode)
        {
        }

        public RecordingHandler(
            Func<CapturedRequest, Task<HttpResponseMessage>> response,
            ResponseIdentityMode identityMode = ResponseIdentityMode.Request)
        {
            this.response = response;
            this.identityMode = identityMode;
        }

        public void ReplaceResponse(Func<CapturedRequest, HttpResponseMessage> next) =>
            response = request => Task.FromResult(next(request));

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(header => header.Key,
                header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
            var fetchOptions = request.Options
                .Where(option => option.Key == "WebAssemblyFetchOptions")
                .Select(option => option.Value as IDictionary<string, object>)
                .SingleOrDefault();
            var credentialsIncluded = fetchOptions is not null &&
                fetchOptions.TryGetValue("credentials", out var credentials) &&
                string.Equals(credentials as string, "include", StringComparison.Ordinal);
            var redirectFailsClosed = fetchOptions is not null &&
                fetchOptions.TryGetValue("redirect", out var redirect) &&
                string.Equals(redirect as string, "error", StringComparison.Ordinal);
            var captured = new CapturedRequest(
                Requests.Count + 1,
                request.Method,
                request.RequestUri!,
                request.RequestUri!.AbsolutePath,
                headers,
                credentialsIncluded,
                redirectFailsClosed);
            Requests.Add(captured);
            var result = await response(captured);
            result.RequestMessage = identityMode switch
            {
                ResponseIdentityMode.Request => request,
                ResponseIdentityMode.MissingRequestUri => new HttpRequestMessage { Method = request.Method },
                _ => null
            };
            return result;
        }
    }

    private enum ResponseIdentityMode
    {
        Request,
        MissingRequestMessage,
        MissingRequestUri
    }

    private sealed record CapturedRequest(
        int Ordinal,
        HttpMethod Method,
        Uri Uri,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        bool CredentialsIncluded,
        bool RedirectFailsClosed);
}
