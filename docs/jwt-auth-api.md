Absolutely — here’s a cleaned-up **AI-agent-ready implementation spec**.

````md
# Aero HTTP Clients + Resilience + JWT/Auth Refactor Spec

## Objective

Refactor Aero HTTP client registration and authentication infrastructure to:

1. Standardize all outbound HTTP clients.
2. Add Microsoft.Extensions.Http.Resilience default resilience behavior.
3. Add reusable `DelegatingHandler`s for cross-cutting HTTP concerns.
4. Replace direct `IConfiguration` base URL lookup with `IOptionsMonitor<AeroHttpClientOptions>`.
5. Add an `AuthClient` typed HTTP client.
6. Add a new Headless JWT API endpoint that validates API keys and returns:
   - JWT access token
   - refresh token
7. Extend `JwtTokenService` with refresh token generation.
8. Investigate moving older JWT-related services into `Aero.Auth`.

---

# 1. Existing Method To Refactor

Current location:

```text
src/Aero.Cms.Abstractions/Http/AeroHttpClientRegistrations.cs
````

Current method:

```csharp
public static class AeroHttpClientExtensions
{
    public static IServiceCollection AddAeroHttpClients(this IServiceCollection services, IConfiguration config)
    {
        var url = config["ApiSettings:BaseUrl"]
            ?? config["AeroHttpClientBaseAddress"];

        ThrowGuard.Throw.IfNullOrEmpty(url, msg: "httpclient url must be valid", argName: nameof(url));
        var uri = new Uri(url);

        services.ConfigureHttpClientDefaults(b =>
        {
            b.AddStandardResilienceHandler();
        });

        services.AddHttpClient<IBlogHttpClient, BlogHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ICategoriesHttpClient, CategoriesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IDashboardHttpClient, DashboardHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IFilesHttpClient, FilesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IMediaHttpClient, MediaHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IModulesHttpClient, ModulesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<INavigationsHttpClient, NavigationsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IPagesHttpClient, PagesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IProfileHttpClient, ProfileHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ISettingsHttpClient, SettingsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ITagsHttpClient, TagsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IThemesHttpClient, ThemesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IUsersHttpClient, UsersHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IBlocksHttpClient, BlocksHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IPublishHttpClient, PublishHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IPreviewHttpClient, PreviewHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IDocsHttpClient, DocsHttpClient>(c => c.BaseAddress = uri);

        return services;
    }
}
```

---

# 2. Add Options Model

Create:

```text
src/Aero.Cms.Abstractions/Http/AeroHttpClientOptions.cs
```

```csharp
namespace Aero.Cms.Abstractions.Http;

public sealed class AeroHttpClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}
```

Expected configuration shape:

```json
{
  "Aero": {
    "HttpClient": {
      "BaseUrl": "https://localhost:333"
    }
  }
}
```

Backward compatibility may be preserved by falling back to:

```text
ApiSettings:BaseUrl
AeroHttpClientBaseAddress
```

But the preferred configuration path is now:

```text
Aero:HttpClient:BaseUrl
```

---

# 3. Add HTTP Delegating Handlers

Create handlers in:

```text
src/Aero.Core/Http/
```

Required handlers:

```text
TenantIdHandler
CorrelationIdHandler
JwtTokenHandler
AeroHttpLoggingHandler
ClientRateLimitHandler
```

Do not place UI-specific login logic inside these handlers.

Handlers must be transport-level concerns only.

---

# 4. Add Required Abstractions

Create in `Aero.Core` or `Aero.Cms.Abstractions`, depending on current dependency flow.

Preferred location:

```text
src/Aero.Core/Http/
```

```csharp
public interface ITokenProvider
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}
```

```csharp
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
```

The `JwtTokenHandler` must depend on `ITokenProvider`.

It must not directly know about:

* Blazor
* MAUI
* login pages
* API keys
* ASP.NET Identity
* refresh-token implementation details

---

# 5. Refactor `AddAeroHttpClients`

Replace the method with an options-based implementation.

```csharp
public static class AeroHttpClientExtensions
{
    public static IServiceCollection AddAeroHttpClients(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<AeroHttpClientOptions>(
            config.GetSection("Aero:HttpClient"));

        services.AddTransient<TenantIdHandler>();
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient<JwtTokenHandler>();
        services.AddTransient<AeroHttpLoggingHandler>();
        services.AddTransient<ClientRateLimitHandler>();

        services.ConfigureHttpClientDefaults(builder =>
        {
            builder
                .AddHttpMessageHandler<CorrelationIdHandler>()
                .AddHttpMessageHandler<TenantIdHandler>()
                .AddHttpMessageHandler<JwtTokenHandler>()
                .AddHttpMessageHandler<AeroHttpLoggingHandler>()
                .AddHttpMessageHandler<ClientRateLimitHandler>()
                .AddStandardResilienceHandler();
        });

        services.AddAeroTypedHttpClient<IBlogHttpClient, BlogHttpClient>();
        services.AddAeroTypedHttpClient<ICategoriesHttpClient, CategoriesHttpClient>();
        services.AddAeroTypedHttpClient<IDashboardHttpClient, DashboardHttpClient>();
        services.AddAeroTypedHttpClient<IFilesHttpClient, FilesHttpClient>();
        services.AddAeroTypedHttpClient<IMediaHttpClient, MediaHttpClient>();
        services.AddAeroTypedHttpClient<IModulesHttpClient, ModulesHttpClient>();
        services.AddAeroTypedHttpClient<INavigationsHttpClient, NavigationsHttpClient>();
        services.AddAeroTypedHttpClient<IPagesHttpClient, PagesHttpClient>();
        services.AddAeroTypedHttpClient<IProfileHttpClient, ProfileHttpClient>();
        services.AddAeroTypedHttpClient<ISettingsHttpClient, SettingsHttpClient>();
        services.AddAeroTypedHttpClient<ITagsHttpClient, TagsHttpClient>();
        services.AddAeroTypedHttpClient<IThemesHttpClient, ThemesHttpClient>();
        services.AddAeroTypedHttpClient<IUsersHttpClient, UsersHttpClient>();
        services.AddAeroTypedHttpClient<IBlocksHttpClient, BlocksHttpClient>();
        services.AddAeroTypedHttpClient<IPublishHttpClient, PublishHttpClient>();
        services.AddAeroTypedHttpClient<IPreviewHttpClient, PreviewHttpClient>();
        services.AddAeroTypedHttpClient<IDocsHttpClient, DocsHttpClient>();

        services.AddAeroTypedHttpClient<IAuthClient, AuthClient>();

        return services;
    }

    private static IHttpClientBuilder AddAeroTypedHttpClient<TClient, TImplementation>(
        this IServiceCollection services)
        where TClient : class
        where TImplementation : class, TClient
    {
        return services.AddHttpClient<TClient, TImplementation>((sp, client) =>
        {
            var options = sp
                .GetRequiredService<IOptionsMonitor<AeroHttpClientOptions>>()
                .CurrentValue;

            var url = options.BaseUrl;

            ThrowGuard.Throw.IfNullOrEmpty(
                url,
                msg: "httpclient url must be valid",
                argName: nameof(options.BaseUrl));

            client.BaseAddress = new Uri(url);
        });
    }
}
```

Required usings:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
```

---

# 6. Important Note About `IOptionsMonitor`

`IOptionsMonitor<AeroHttpClientOptions>` does not update the `BaseAddress` of an already-created typed `HttpClient`.

It only affects newly created typed clients.

This is acceptable for now.

Do not attempt to mutate existing `HttpClient.BaseAddress` dynamically.

---

# 7. Handler Details

## 7.1 TenantIdHandler

Purpose:

* Add tenant/site headers for routing, diagnostics, and logging.
* Do not treat these headers as authorization.

```csharp
public sealed class TenantIdHandler : DelegatingHandler
{
    private readonly ISiteContext _siteContext;

    public TenantIdHandler(ISiteContext siteContext)
    {
        _siteContext = siteContext;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(
            "X-Tenant-Id",
            _siteContext.TenantId.ToString());

        request.Headers.TryAddWithoutValidation(
            "X-Site-Id",
            _siteContext.SiteId.ToString());

        return base.SendAsync(request, cancellationToken);
    }
}
```

Important:

```text
X-Tenant-Id and X-Site-Id are not security boundaries.
The API must still validate the JWT/API key claims.
```

---

## 7.2 CorrelationIdHandler

```csharp
public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _accessor;

    public CorrelationIdHandler(ICorrelationIdAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _accessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation(
                "X-Correlation-Id",
                correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```

---

## 7.3 JwtTokenHandler

```csharp
public sealed class JwtTokenHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;

    public JwtTokenHandler(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

Required using:

```csharp
using System.Net.Http.Headers;
```

---

## 7.4 AeroHttpLoggingHandler

```csharp
public sealed class AeroHttpLoggingHandler : DelegatingHandler
{
    private readonly ILogger<AeroHttpLoggingHandler> _logger;

    public AeroHttpLoggingHandler(ILogger<AeroHttpLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "HTTP request started: {Method} {Uri}",
            request.Method,
            request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken);

        _logger.LogInformation(
            "HTTP request completed: {Method} {Uri} {StatusCode}",
            request.Method,
            request.RequestUri,
            response.StatusCode);

        return response;
    }
}
```

Do not log:

```text
Authorization headers
JWT tokens
API keys
cookies
refresh tokens
raw request bodies
raw response bodies
```

---

## 7.5 ClientRateLimitHandler

Initial simple implementation:

```csharp
public sealed class ClientRateLimitHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim Semaphore = new(20);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await Semaphore.WaitAsync(cancellationToken);

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
```

Future improvement:

* Move the concurrency limit to options.
* Consider `System.Threading.RateLimiting`.
* Do not duplicate retry behavior already handled by resilience.

---

# 8. Add AuthClient

Create:

```text
src/Aero.Cms.Abstractions/Http/Clients/AuthClient.cs
```

Interface:

```text
src/Aero.Cms.Abstractions/Http/Clients/IAuthClient.cs
```

Responsibilities:

```text
- Login with username/password for CMS Manager.
- Login with API key for headless clients.
- Receive JWT access token.
- Receive refresh token.
- Refresh access token using refresh token.
```

Suggested contracts:

```csharp
public sealed record LoginRequest(
    string UserName,
    string Password);

public sealed record ApiKeyLoginRequest(
    string ApiKey);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record JwtTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
```

Suggested interface:

```csharp
public interface IAuthClient
{
    Task<JwtTokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<JwtTokenResponse> LoginWithApiKeyAsync(
        ApiKeyLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<JwtTokenResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
```

---

# 9. Add Headless JWT API

Create:

```text
src/Aero.Cms.Modules.Headless/Api/v1/JwtApi.cs
```

Responsibilities:

```text
- Accept API key login request.
- Validate API key using IApiKeyService / ApiKeyService.
- If valid, generate JWT access token.
- Generate refresh token.
- Return both tokens.
- Add refresh endpoint.
```

Suggested endpoints:

```text
POST /api/v1/headless/jwt/token
POST /api/v1/headless/jwt/refresh
```

Suggested request/response models:

```csharp
public sealed record HeadlessJwtRequest(
    string ApiKey);

public sealed record HeadlessRefreshTokenRequest(
    string RefreshToken);

public sealed record HeadlessJwtResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
```

Validation requirements:

```text
- API key must exist.
- API key must not be revoked.
- API key must not be expired.
- API key must belong to a valid tenant/site.
- API key scopes must allow headless access.
```

JWT claims should include:

```text
sub
tenant_id
site_id
api_key_id
scope
token_use = access
```

Refresh-token storage must include:

```text
token hash
api key id
tenant id
site id
expires at
created at
revoked at
replaced by token id
```

Do not store raw refresh tokens.

---

# 10. Update JwtTokenService

Primary file:

```text
src/Aero.Auth/Services/JwtTokenService.cs
```

Add refresh token generation.

Required methods:

```csharp
public string GenerateAccessToken(...);

public RefreshTokenResult GenerateRefreshToken(...);
```

Suggested result model:

```csharp
public sealed record RefreshTokenResult(
    string Token,
    string TokenHash,
    DateTimeOffset ExpiresAt);
```

Refresh token generation requirements:

```text
- Use RandomNumberGenerator.
- Generate at least 32 random bytes.
- Return raw token only once to caller.
- Store only hash of token.
- Use SHA256 or stronger hash for persistence.
```

Example:

```csharp
public RefreshTokenResult GenerateRefreshToken(TimeSpan lifetime)
{
    var bytes = RandomNumberGenerator.GetBytes(64);
    var token = WebEncoders.Base64UrlEncode(bytes);

    var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    var hash = Convert.ToHexString(hashBytes);

    return new RefreshTokenResult(
        token,
        hash,
        DateTimeOffset.UtcNow.Add(lifetime));
}
```

Required usings:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
```

> [!TIP]
> When implementing Section 10 (JwtTokenService), ensure that the SHA256 hashing uses a consistent encoding (like UTF8) and hex/base64 representation to avoid issues when validating hashes later. The spec's example using WebEncoders.Base64UrlEncode for the raw token and hex for the hash is a solid choice.

---

# 11. Investigate Older JWT Files

There are two important JWT files:

```text
src/Aero.Services/JwtTokenBuilder.cs
```

and

```text
src/Aero.Auth/Services/JwtTokenService.cs
```

Likely current service:

```text
src/Aero.Auth/Services/JwtTokenService.cs
```

Likely older service:

```text
src/Aero.Services/JwtTokenBuilder.cs
```

Agent task:

```text
Analyze both files before moving or deleting anything.
```

Rules:

```text
- Do not delete JwtTokenBuilder.cs blindly.
- Determine whether it is still referenced.
- Determine whether it contains useful token-building logic.
- If it is unused and redundant, move or consolidate into Aero.Auth only if it improves the design.
- If useful, adapt the logic into JwtTokenService.
- Avoid duplicate JWT generation paths.
```

Preferred final state:

```text
Aero.Auth owns JWT token generation.
Aero.Services should not own JWT-building logic unless there is a clear dependency reason.
```

---

# 12. Investigate Moving AeroJwtValidationService

Current file:

```text
src/Aero.Services/AeroJwtValidationService.cs
```

Agent task:

```text
Investigate whether this should move to Aero.Auth.
```

Move only if:

```text
- Aero.Auth can reference required dependencies without causing circular references.
- Validation logically belongs beside JwtTokenService.
- Existing consumers can be updated cleanly.
```

Preferred final state:

```text
Aero.Auth should own:
- JWT generation
- JWT validation
- refresh token generation
- API key generation abstractions
```

Avoid:

```text
- Circular project references
- Duplicating JWT validation
- Breaking existing DI registration
```

---

# 13. API Key Generator

Add to:

```text
src/Aero.Auth/
```

Create:

```csharp
public interface IApiKeyGenerator
{
    GeneratedApiKey Generate(ApiKeyEnvironment environment);
}
```

```csharp
public enum ApiKeyEnvironment
{
    Test,
    Live
}
```

```csharp
public sealed record GeneratedApiKey(
    string KeyId,
    string RawApiKey,
    string SecretHash);
```

Implementation:

```csharp
public sealed class HashedApiKeyGenerator : IApiKeyGenerator
{
    public GeneratedApiKey Generate(ApiKeyEnvironment environment)
    {
        var prefix = environment == ApiKeyEnvironment.Live
            ? "sk_live"
            : "sk_test";

        var keyId = Guid.NewGuid().ToString("N");

        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToHexString(secretBytes).ToLowerInvariant();

        var rawApiKey = $"{prefix}_{keyId}_{secret}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey));
        var secretHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new GeneratedApiKey(
            keyId,
            rawApiKey,
            secretHash);
    }
}
```

Important:

```text
The raw API key is returned once.
Only the hash is stored.
```

---

# 14. Login.razor.cs Update

Locate:

```text
Aero.Cms.Shared/Pages/Manager/Login.razor
Aero.Cms.Shared/Pages/Manager/Login.razor.cs
```

Agent tasks:

```text
- Confirm whether Login.razor already exists.
- Inject IAuthClient.
- On login submit, call IAuthClient.LoginAsync.
- Store returned JWT access token in an in-memory token provider.
- Store refresh token in memory only for now.
- Do not store JWT in localStorage.
- Do not store JWT in cookies.
```

Development mode behavior:

```text
If environment is Development, optionally pre-populate the login form with seeded admin credentials.
Do not do this in Production.
```

Authorization:

```text
After login, ensure the authenticated user has one of the required Aero CMS roles.
```

Investigate:

```text
AeroRoles.cs
AeroCmsRoles
Seeded admin user
Seeded admin role assignment
```

---

# 15. Token Storage Rule

For CMS Manager:

```text
Access token: memory only
Refresh token: memory only for initial implementation
```

For headless clients:

```text
They receive access token and refresh token from JwtApi.
They are responsible for storing them securely.
```

Do not store tokens in:

```text
localStorage
sessionStorage
plain cookies
logs
query strings
```

---

# 16. Resilience Requirements

Use:

```csharp
AddStandardResilienceHandler()
```

This should be added globally via:

```csharp
ConfigureHttpClientDefaults
```

Do not manually add duplicate retry handlers unless there is a specific reason.

Potential future customizations:

```text
- total request timeout
- attempt timeout
- retry count
- retry backoff
- circuit breaker
- 429 handling
```

But for this task, start with default Microsoft resilience.

---

# 17. Package Requirements

Ensure required packages exist where needed:

```text
Microsoft.Extensions.Http.Resilience
Microsoft.Extensions.Options
```

For refresh token generation:

```text
Microsoft.AspNetCore.WebUtilities
```

Only add package references to projects that need them.

---

# 18. Acceptance Criteria

## HTTP Client Registration

* `AddAeroHttpClients` no longer directly uses `config["ApiSettings:BaseUrl"]` as the primary mechanism.
* `AeroHttpClientOptions` exists.
* Typed clients use `IOptionsMonitor<AeroHttpClientOptions>`.
* All existing typed clients are still registered.
* `IAuthClient/AuthClient` is registered.
* `AddStandardResilienceHandler()` is applied globally.
* Delegating handlers are registered and included globally.

## Handlers

* `TenantIdHandler` compiles.
* `CorrelationIdHandler` compiles.
* `JwtTokenHandler` compiles.
* `AeroHttpLoggingHandler` compiles.
* `ClientRateLimitHandler` compiles.
* Handlers do not contain UI-specific logic.
* Handlers do not log secrets.

## Auth

* `JwtApi` exists in `Aero.Cms.Modules.Headless/Api/v1`.
* API key login endpoint exists.
* Refresh endpoint exists.
* API key is validated through existing API key service.
* JWT access token is returned.
* Refresh token is returned.
* Refresh token raw value is only returned once.
* Refresh token hash is what gets persisted.

## JWT Services

* `JwtTokenService` supports refresh token generation.
* Duplicate JWT-building logic is reduced if safe.
* `JwtTokenBuilder.cs` is analyzed before being moved or deleted.
* `AeroJwtValidationService.cs` is analyzed before being moved.

## UI

* `Login.razor.cs` uses `IAuthClient`.
* JWT is stored in memory only.
* Development-only seeded login autofill is guarded by environment check.
* Role authorization is verified.

## Build

* Solution compiles.
* Do not change unrelated business logic.
* Do not introduce circular project references.
* Do not remove existing behavior unless replaced by the new implementation.

---

# 19. Explicit Non-Goals

Do not implement:

```text
- Full OAuth/OpenIddict server
- Cookie auth migration
- Persistent browser token storage
- LocalStorage token storage
- Full refresh-token rotation UI
- MAUI secure storage
- YARP gateway changes
```

These can come later.

---

# 20. Key Design Rule

Keep the boundaries clean:

```text
DelegatingHandlers = outbound HTTP transport concerns

AuthClient = talks to auth endpoints

JwtApi = issues tokens for headless/API-key clients

JwtTokenService = creates and validates tokens

ITokenProvider = supplies current token to outbound HTTP clients

Login.razor.cs = UI workflow only
```

```
```
