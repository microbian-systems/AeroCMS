using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.EntraExternalId;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class EntraExternalIdProviderTests
{
    private const string OrganizationId = "11111111-2222-3333-4444-555555555555";
    private const string ClientId = "22222222-3333-4444-5555-666666666666";
    private const string Authority = "https://contoso.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0";
    private const string Issuer = "https://11111111-2222-3333-4444-555555555555.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0";

    [Test]
    public async Task Authorization_uses_exact_oidc_pkce_parameters_and_never_discloses_secret_or_return_path()
    {
        using var rsa = RSA.Create(2048);
        var setup = Setup(rsa, _ => "{}", validKeys: true);
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var challenge = Ok(await setup.Strategy.CreateAuthorizationAsync(setup.Begin, prepared,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", credentials));
        var parameters = QueryHelpers.ParseQuery(new Uri(challenge.Target).Query);

        await Assert.That(challenge.Target.StartsWith(Authority[..^5] + "/oauth2/v2.0/authorize?", StringComparison.Ordinal)).IsTrue();
        await Assert.That(challenge.Parameters).IsEmpty();
        await Assert.That(parameters["state"].Single()).IsEqualTo("12345678901234567890.abcdefghijklmnopqrstuvwxyz");
        await Assert.That(parameters["response_mode"].Single()).IsEqualTo("query");
        await Assert.That(parameters["scope"].Single()).IsEqualTo("openid profile email");
        await Assert.That(parameters["code_challenge_method"].Single()).IsEqualTo("S256");
        await Assert.That(challenge.Target.Contains("client_secret", StringComparison.Ordinal)).IsFalse();
        await Assert.That(challenge.Target.Contains("%2Forders", StringComparison.Ordinal)).IsFalse();
        await Assert.That(challenge.Target.Contains("offline_access", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Callback_validates_signed_token_and_only_exposes_boolean_verified_email()
    {
        using var rsa = RSA.Create(2048);
        string nonce = string.Empty;
        var setup = Setup(rsa, _ => TokenReply(CreateToken(rsa, nonce, true)), validKeys: true);
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var challenge = Ok(await setup.Strategy.CreateAuthorizationAsync(setup.Begin, prepared,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", credentials));
        nonce = QueryHelpers.ParseQuery(new Uri(challenge.Target).Query)["nonce"].Single();

        var result = Ok(await setup.Strategy.AuthenticateAsync(new(setup.Authority, 3, setup.Begin.CallbackUri,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", prepared.ProtectedProviderCorrelation,
            "single-use-code", null, null, null), credentials));

        await Assert.That(result.Subject).IsEqualTo("external-subject");
        await Assert.That(result.Email).IsEqualTo("member@example.test");
        await Assert.That(result.EmailVerified).IsTrue();
        await Assert.That(result.ProviderSessionReference).IsNull();
        await Assert.That(setup.Handler.TokenCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Callback_treats_non_boolean_email_verified_as_unverified()
    {
        using var rsa = RSA.Create(2048);
        string nonce = string.Empty;
        var setup = Setup(rsa, _ => TokenReply(CreateToken(rsa, nonce, "true")), validKeys: true);
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var challenge = Ok(await setup.Strategy.CreateAuthorizationAsync(setup.Begin, prepared,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", credentials));
        nonce = QueryHelpers.ParseQuery(new Uri(challenge.Target).Query)["nonce"].Single();

        var result = await setup.Strategy.AuthenticateAsync(new(setup.Authority, 3, setup.Begin.CallbackUri,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", prepared.ProtectedProviderCorrelation,
            "single-use-code", null, null, null), credentials);

        var identity = Ok(result);
        await Assert.That(identity.EmailVerified).IsFalse();
        await Assert.That(identity.Email).IsNull();
    }

    [Test]
    public async Task Callback_refreshes_signing_keys_once_without_redeeming_code_again()
    {
        using var rsa = RSA.Create(2048);
        string nonce = string.Empty;
        var setup = Setup(rsa, _ => TokenReply(CreateToken(rsa, nonce, true)), validKeys: false);
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var challenge = Ok(await setup.Strategy.CreateAuthorizationAsync(setup.Begin, prepared,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", credentials));
        nonce = QueryHelpers.ParseQuery(new Uri(challenge.Target).Query)["nonce"].Single();

        var result = await setup.Strategy.AuthenticateAsync(new(setup.Authority, 3, setup.Begin.CallbackUri,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", prepared.ProtectedProviderCorrelation,
            "single-use-code", null, null, null), credentials);

        await Assert.That(result).IsTypeOf<Result<ValidatedExternalIdentity, AeroError>.Ok>();
        await Assert.That(setup.Configuration.Refreshes).IsEqualTo(1);
        await Assert.That(setup.Handler.TokenCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Correlation_is_bound_to_callback_and_client_id_and_rejects_tampering()
    {
        using var rsa = RSA.Create(2048);
        var setup = Setup(rsa, _ => "{}", validKeys: true);
        using var credentials = Credentials();
        var prepared = Ok(await setup.Strategy.PrepareAuthorizationAsync(setup.Begin, credentials));
        var tampered = prepared with { ProtectedProviderCorrelation = prepared.ProtectedProviderCorrelation[..^1] + "A" };

        var result = await setup.Strategy.CreateAuthorizationAsync(setup.Begin, tampered,
            "12345678901234567890.abcdefghijklmnopqrstuvwxyz", credentials);
        await Assert.That(result).IsTypeOf<Result<ExternalProviderAuthorizationChallenge, AeroError>.Failure>();
    }

    private static SetupResult Setup(RSA rsa, Func<HttpRequestMessage, string> reply, bool validKeys)
    {
        var goodKey = new RsaSecurityKey(rsa) { KeyId = "current" };
        var staleRsa = RSA.Create(2048);
        var staleKey = new RsaSecurityKey(staleRsa) { KeyId = "stale" };
        var configuration = new RecordingConfigurationSource(Configuration(validKeys ? goodKey : staleKey), Configuration(goodKey));
        var handler = new RecordingHandler(reply);
        var http = new EntraExternalIdHttpClient(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) });
        var strategy = new EntraExternalIdProviderStrategy(new EphemeralDataProtectionProvider(), configuration,
            http, TimeProvider.System);
        var authority = new ExternalProviderAuthority(1, 2, ExternalMemberProviders.EntraExternalId, Issuer,
            OrganizationId, Authority, new(1, "test", 2, ExternalMemberProviders.EntraExternalId, "/x"));
        var begin = new ExternalProviderBeginContext(authority, 3,
            new Uri("https://store.example.test/api/v1/member/callback"), "/orders");
        return new(strategy, handler, configuration, authority, begin, staleRsa);
    }

    private static OpenIdConnectConfiguration Configuration(SecurityKey key) => new()
    {
        Issuer = Issuer,
        AuthorizationEndpoint = Authority[..^5] + "/oauth2/v2.0/authorize",
        TokenEndpoint = Authority[..^5] + "/oauth2/v2.0/token",
        JwksUri = Authority[..^5] + "/discovery/v2.0/keys",
        EndSessionEndpoint = Authority[..^5] + "/oauth2/v2.0/logout",
        SigningKeys = { key }
    };

    private static string CreateToken(RSA rsa, string nonce, object emailVerified)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ClientId,
            IssuedAt = now,
            NotBefore = now.AddSeconds(-5),
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "current" }, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "external-subject",
                ["tid"] = OrganizationId,
                ["nonce"] = nonce,
                ["iat"] = new DateTimeOffset(now).ToUnixTimeSeconds(),
                ["email"] = "member@example.test",
                ["email_verified"] = emailVerified,
                ["name"] = "External Member"
            }
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string TokenReply(string token) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["token_type"] = "Bearer", ["id_token"] = token, ["access_token"] = "discard-me",
        ["refresh_token"] = "discard-me-too", ["expires_in"] = 300
    });

    private static ExternalProviderCredentialBundle Credentials() => new(
        Encoding.UTF8.GetBytes(ClientId), Encoding.UTF8.GetBytes("client-secret"), null);
    private static T Ok<T>(Result<T, AeroError> result) => result is Result<T, AeroError>.Ok(var value)
        ? value : throw new InvalidOperationException("Expected success.");

    private sealed class RecordingConfigurationSource(OpenIdConnectConfiguration initial, OpenIdConnectConfiguration refreshed)
        : IEntraOpenIdConfigurationSource
    {
        public int Refreshes { get; private set; }
        public Task<OpenIdConnectConfiguration> GetAsync(string authority, string organizationId, bool refresh,
            CancellationToken cancellationToken)
        {
            if (refresh) Refreshes++;
            return Task.FromResult(refresh ? refreshed : initial);
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, string> reply) : HttpMessageHandler
    {
        public int TokenCalls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            TokenCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply(request), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record SetupResult(EntraExternalIdProviderStrategy Strategy, RecordingHandler Handler,
        RecordingConfigurationSource Configuration, ExternalProviderAuthority Authority,
        ExternalProviderBeginContext Begin, RSA StaleRsa);
}
