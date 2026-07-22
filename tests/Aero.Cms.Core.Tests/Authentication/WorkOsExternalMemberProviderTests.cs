using System.Net;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.WorkOS;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class WorkOsExternalMemberProviderTests
{
    [Test]
    public async Task Authorization_and_authentication_use_bound_pkce_and_fixed_workos_endpoint()
    {
        var handler = new CaptureHandler("""{"user":{"id":"user_1","email":"a@example.test","email_verified":true,"first_name":"Ada","last_name":"Lovelace"},"organization_id":"org_123","access_token":"e30.eyJzaWQiOiJzZXNzXzEifQ.sig"}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.workos.com/"), Timeout = TimeSpan.FromSeconds(10) };
        var strategy = new WorkOsExternalMemberProviderStrategy(new WorkOsAuthenticationClient(http), DataProtectionProvider.Create("workos-test"), TimeProvider.System);
        using var credentials = new ExternalProviderCredentialBundle(Encoding.UTF8.GetBytes("client_123"), null, Encoding.UTF8.GetBytes("sk_test"));
        var authority = new ExternalProviderAuthority(1, 2, ExternalMemberProviders.WorkOs, "https://api.workos.com", "org_123", "https://api.workos.com", new(1, "test", 2, "workos", "/x"));
        var begin = new ExternalProviderBeginContext(authority, 3, new Uri("https://store.example.test/signin/workos/callback"), "/shop");
        var handle = "123." + new string('a', 43);

        var prepared = Ok(await strategy.PrepareAuthorizationAsync(begin, credentials));
        var challenge = Ok(await strategy.CreateAuthorizationAsync(begin, prepared, handle, credentials));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(challenge.Target).Query);
        await Assert.That(query["state"].ToString()).IsEqualTo(handle);
        await Assert.That(query["code_challenge_method"].ToString()).IsEqualTo("S256");

        var identity = Ok(await strategy.AuthenticateAsync(new(authority, 3, begin.CallbackUri, handle, prepared.ProtectedProviderCorrelation, "code_123", null, null, null), credentials));
        await Assert.That(identity.Subject).IsEqualTo("user_1");
        await Assert.That(identity.ProviderSessionReference).IsEqualTo("sess_1");
        await Assert.That(handler.Request!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.Request.RequestUri!.AbsolutePath).IsEqualTo("/user_management/authenticate");
        await Assert.That(handler.Body).Contains("\"code_verifier\"");
        var verifier = System.Text.Json.JsonDocument.Parse(handler.Body!).RootElement.GetProperty("code_verifier").GetString()!;
        await Assert.That(WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))).IsEqualTo(query["code_challenge"].ToString());
    }

    [Test]
    public async Task Invalid_callback_does_not_send_http_and_logout_is_fixed()
    {
        var handler = new CaptureHandler("{}");
        var strategy = new WorkOsExternalMemberProviderStrategy(new WorkOsAuthenticationClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.workos.com/") }), DataProtectionProvider.Create("workos-test-2"), TimeProvider.System);
        using var credentials = new ExternalProviderCredentialBundle(Encoding.UTF8.GetBytes("client_123"), null, Encoding.UTF8.GetBytes("sk_test"));
        var authority = new ExternalProviderAuthority(1, 2, ExternalMemberProviders.WorkOs, "https://api.workos.com", "org_123", "https://api.workos.com", new(1, "test", 2, "workos", "/x"));
        var callback = new ExternalProviderCallbackContext(authority, 3, new Uri("https://store.example.test/callback"), "bad", "bad", null, "access_denied", null, null);
        var result = await strategy.AuthenticateAsync(callback, credentials);
        await Assert.That(result).IsTypeOf<Result<ValidatedExternalIdentity, AeroError>.Failure>();
        await Assert.That(handler.Request).IsNull();
        var logout = Ok(await strategy.PrepareLogoutAsync(new(authority, 3, new Uri("https://store.example.test/logout"), "sess_1"), credentials));
        await Assert.That(logout.Target).IsEqualTo("https://api.workos.com/user_management/sessions/logout?session_id=sess_1&return_to=https%3A%2F%2Fstore.example.test%2Flogout");
    }

    [Test]
    public async Task Tampered_correlation_fails_before_authenticate_http()
    {
        var setup = Create("""{"user":{"id":"user_1","email":"a@example.test","email_verified":true},"organization_id":"org_123","access_token":"not-a-jwt"}""");
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var tampered = prepared with { ProtectedProviderCorrelation = prepared.ProtectedProviderCorrelation[..^1] + "x" };
        var result = await setup.Strategy.AuthenticateAsync(new(setup.Authority, 3, setup.Begin.CallbackUri, "123." + new string('a', 43), tampered.ProtectedProviderCorrelation, "code", null, null, null), credentials);
        await Assert.That(result).IsTypeOf<Result<ValidatedExternalIdentity, AeroError>.Failure>();
        await Assert.That(setup.Handler.Request).IsNull();
    }

    [Test]
    [Arguments("org")]
    [Arguments("impersonator")]
    [Arguments("malformed")]
    [Arguments("non-success")]
    [Arguments("oversized")]
    public async Task Unsafe_or_invalid_provider_responses_fail_closed(string scenario)
    {
        var body = scenario switch
        {
            "org" => """{"user":{"id":"user_1","email":"a@example.test","email_verified":true},"organization_id":"org_wrong","access_token":"not-a-jwt"}""",
            "impersonator" => """{"user":{"id":"user_1","email":"a@example.test","email_verified":true},"organization_id":"org_123","access_token":"not-a-jwt","impersonator":{"id":"x"}}""",
            "malformed" => "{",
            _ => """{"user":{"id":"user_1","email":"a@example.test","email_verified":true},"organization_id":"org_123","access_token":"not-a-jwt"}"""
        };
        var setup = Create(body, scenario == "non-success" ? HttpStatusCode.BadGateway : HttpStatusCode.OK, scenario == "oversized" ? 1024 * 1024 + 1 : null);
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var result = await setup.Strategy.AuthenticateAsync(new(setup.Authority, 3, setup.Begin.CallbackUri, "123." + new string('a', 43), prepared.ProtectedProviderCorrelation, "code", null, null, null), credentials);
        await Assert.That(result).IsTypeOf<Result<ValidatedExternalIdentity, AeroError>.Failure>();
    }

    [Test]
    public async Task Missing_or_malformed_sid_does_not_prevent_identity_mapping()
    {
        var setup = Create("""{"user":{"id":"user_1","email":"a@example.test","email_verified":true},"organization_id":"org_123","access_token":"not-a-jwt"}""");
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var identity = Ok(await setup.Strategy.AuthenticateAsync(new(setup.Authority, 3, setup.Begin.CallbackUri, "123." + new string('a', 43), prepared.ProtectedProviderCorrelation, "code", null, null, null), credentials));
        await Assert.That(identity.ProviderSessionReference).IsNull();
    }

    private static T Ok<T>(Result<T, AeroError> r) => r is Result<T, AeroError>.Ok(var value) ? value : throw new InvalidOperationException();
    private static ExternalProviderCredentialBundle Credentials() => new(Encoding.UTF8.GetBytes("client_123"), null, Encoding.UTF8.GetBytes("sk_test"));
    private static Setup Create(string body, HttpStatusCode status = HttpStatusCode.OK, int? length = null)
    {
        var handler = new CaptureHandler(body, status, length);
        var authority = new ExternalProviderAuthority(1, 2, ExternalMemberProviders.WorkOs, "https://api.workos.com", "org_123", "https://api.workos.com", new(1, "test", 2, "workos", "/x"));
        return new(handler, authority, new(authority, 3, new Uri("https://store.example.test/callback"), "/"), new WorkOsExternalMemberProviderStrategy(new WorkOsAuthenticationClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.workos.com/") }), DataProtectionProvider.Create(Guid.NewGuid().ToString("N")), TimeProvider.System));
    }
    private sealed record Setup(CaptureHandler Handler, ExternalProviderAuthority Authority, ExternalProviderBeginContext Begin, WorkOsExternalMemberProviderStrategy Strategy);
    private sealed class CaptureHandler(string response, HttpStatusCode status = HttpStatusCode.OK, int? length = null) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Request = request; Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken); var content = new StringContent(response, Encoding.UTF8, "application/json"); if (length.HasValue) content.Headers.ContentLength = length; return new(status) { Content = content }; }
    }
}
