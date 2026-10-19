using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NSubstitute;
using TUnit.Core;

namespace Aero.Cms.E2E.Tests;

/// <summary>
/// Browser proof for the same-origin manager antiforgery handshake.  The host
/// uses generated Web SDK static assets and synthetic loopback authentication;
/// it never connects to a CMS database or a live identity provider.
/// </summary>
[NotInParallel]
public sealed class SameOriginExternalIdentityAdminWasmTests
{
    [Test]
    public async Task Manager_wasm_uses_a_fresh_same_origin_antiforgery_header_for_the_real_authority_mutation()
    {
        var scope = Substitute.For<IExternalIdentityManagerScopeResolver>();
        scope.ResolveAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<Result<ExternalIdentityManagerScope, AeroError>>(
                Prelude.Ok<ExternalIdentityManagerScope, AeroError>(new(701L, 702L))));
        var authority = Substitute.For<IExternalIdentityAuthorityService>();
        authority.ConfigureAsync(Arg.Any<ExternalIdentityManagerScope>(), Arg.Any<ConfigureExternalIdentityAuthorityRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ExternalIdentityAuthorityResult, AeroError>>(
                Prelude.Ok<ExternalIdentityAuthorityResult, AeroError>(new(703L, 701L, "workos", "https://api.workos.com", "org_e2e", "https://api.workos.com", 704L, "test", true))));

        await using var host = await SyntheticManagerHost.StartAsync(scope, authority);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block });
        var page = await context.NewPageAsync();
        var consoleErrors = new ConcurrentQueue<ConsoleError>();
        var pageErrors = new ConcurrentQueue<string>();
        var failedResponses = new ConcurrentQueue<FailedResponse>();
        var failedRequests = new ConcurrentQueue<FailedRequest>();
        var shellResponses = new ConcurrentQueue<int>();
        var observations = new ConcurrentQueue<RequestObservation>();
        var frameworkRequests = new ConcurrentQueue<string>();
        long requestSequence = 0;
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
                consoleErrors.Enqueue(new(ClassifyConsoleError(message.Text), SafeLocation(message.Location, host.BaseUri), ComponentHint(message.Text), FrameworkHint(message.Text)));
        };
        page.PageError += (_, _) => pageErrors.Enqueue("page-error");
        page.RequestFailed += (_, request) => failedRequests.Enqueue(new(SafeLocation(request.Url, host.BaseUri), ClassifyRequestFailure(request.Failure), request.Method));
        page.Response += (_, response) =>
        {
            var uri = new Uri(response.Url);
            if (SafeLocation(response.Url, host.BaseUri) == "/api/v1/admin/sites/current" && response.Request.Method == "POST")
                shellResponses.Enqueue(response.Status);
            if (uri.Scheme == host.BaseUri.Scheme && uri.Host == host.BaseUri.Host && uri.Port == host.BaseUri.Port && response.Status >= StatusCodes.Status400BadRequest)
                failedResponses.Enqueue(new(uri.AbsolutePath, response.Status));
        };
        page.Request += (_, request) =>
        {
            var uri = new Uri(request.Url);
            var sameOrigin = uri.Scheme == host.BaseUri.Scheme && uri.Host == host.BaseUri.Host && uri.Port == host.BaseUri.Port;
            var path = sameOrigin ? uri.AbsolutePath : "foreign";
            if (sameOrigin && path.StartsWith("/_framework/", StringComparison.Ordinal))
                frameworkRequests.Enqueue(path);
            if (path is "/api/v1/admin/external-identity/antiforgery" or "/api/v1/admin/external-identity/authority")
                observations.Enqueue(new(Interlocked.Increment(ref requestSequence), request.Method, path,
                    request.Headers.ContainsKey("x-csrf-token"),
                    request.Headers.ContainsKey("requestverificationtoken")));
        };

        var anonymousHandshake = await context.APIRequest.GetAsync(new Uri(host.BaseUri, "api/v1/admin/external-identity/antiforgery").ToString());
        anonymousHandshake.Status.Should().Be(StatusCodes.Status401Unauthorized);
        var anonymousAuthority = await context.APIRequest.GetAsync(new Uri(host.BaseUri, "api/v1/admin/external-identity/authority").ToString());
        anonymousAuthority.Status.Should().Be(StatusCodes.Status401Unauthorized);

        var signIn = await context.APIRequest.GetAsync(
            new Uri(host.BaseUri, "__e2e/sign-in").ToString(),
            new() { MaxRedirects = 0 });
        signIn.Status.Should().Be(StatusCodes.Status302Found);
        var initialResponse = await context.APIRequest.GetAsync(new Uri(host.BaseUri, "manager/external-members").ToString());
        (await initialResponse.TextAsync()).Should().NotContain("external-authority");
        await page.GotoAsync(new Uri(host.BaseUri, "manager/external-members").ToString(), new() { WaitUntil = WaitUntilState.Load });

        // Authenticated manager pages deliberately have no component prerender;
        // the form appears only after the actual WebAssembly client has booted.
        await page.GetByRole(AriaRole.Button, new() { Name = "Save authority", Exact = true })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });

        await page.GetByLabel("Organization identifier").FillAsync("org_e2e");
        await page.GetByLabel("Aero.Vault ID").FillAsync("704");
        await page.GetByLabel("Vault environment").FillAsync("test");
        var put = page.WaitForResponseAsync(response =>
            response.Request.Method == "PUT" &&
            new Uri(response.Url).AbsolutePath == "/api/v1/admin/external-identity/authority");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save authority", Exact = true }).ClickAsync();
        (await put).Status.Should().Be(200);
        (await page.GetByLabel("Enabled").IsCheckedAsync()).Should().BeTrue();
        (await page.GetByLabel("Organization identifier").IsDisabledAsync()).Should().BeTrue();

        await authority.Received(1).ConfigureAsync(
            new ExternalIdentityManagerScope(701L, 702L),
            Arg.Is<ConfigureExternalIdentityAuthorityRequest>(request =>
                request.Provider == "workos" && request.OrganizationId == "org_e2e" && request.VaultId == 704L && request.VaultEnvironment == "test"),
            Arg.Any<CancellationToken>());
        var api = observations.ToArray();
        var handshake = api.Single(value => value is { Method: "GET", Path: "/api/v1/admin/external-identity/antiforgery" });
        var mutation = api.Single(value => value is { Method: "PUT", Path: "/api/v1/admin/external-identity/authority" });
        handshake.Sequence.Should().BeLessThan(mutation.Sequence);
        mutation.HasCsrfHeader.Should().BeTrue();
        mutation.HasLegacyHeader.Should().BeFalse();
        var wasmBoot = frameworkRequests.ToArray();
        wasmBoot.Should().Contain(path =>
            path.StartsWith("/_framework/blazor.web", StringComparison.Ordinal) &&
            path.EndsWith(".js", StringComparison.Ordinal));
        wasmBoot.Should().Contain(path =>
            path.StartsWith("/_framework/dotnet", StringComparison.Ordinal) &&
            path.EndsWith(".js", StringComparison.Ordinal));
        wasmBoot.Should().Contain(path =>
            path.StartsWith("/_framework/Aero.Cms.Web.Client", StringComparison.Ordinal) &&
            path.EndsWith(".wasm", StringComparison.Ordinal));
        var shellCompletion = await host.SiteSelectionCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using (new AssertionScope())
        {
            failedResponses.Should().BeEmpty();
            // Chromium can cancel reading this empty shell response after its
            // headers arrive. Accept only that exact observation, and only with
            // independent browser and server proof of successful completion.
            shellResponses.Should().Equal(StatusCodes.Status200OK);
            shellCompletion.Should().Be(new SiteSelectionCompletion(702L, StatusCodes.Status200OK, true, false));
            var failures = failedRequests.ToArray();
            if (failures.Length != 0)
                failures.Should().Equal(new FailedRequest("/api/v1/admin/sites/current", "aborted", "POST"));
            consoleErrors.Should().BeEmpty();
            pageErrors.Should().BeEmpty();
        }
    }

    private sealed record RequestObservation(long Sequence, string Method, string Path, bool HasCsrfHeader, bool HasLegacyHeader);
    private sealed record FailedResponse(string Path, int Status);
    private sealed record FailedRequest(string Location, string Category, string Method);
    private sealed record ConsoleError(string Category, string Location, string Component, string Framework);
    private sealed record SiteSelectionCompletion(long SiteId, int Status, bool HasStarted, bool RequestAborted);

    private static readonly (string Marker, string Category)[] ConsoleErrorMarkers =
    [
        ("Failed to fetch", "resource-fetch"),
        ("Failed to load resource", "resource-fetch"),
        ("MIME", "mime"),
        ("import", "import"),
        ("AbortError", "abort"),
        ("InvalidOperationException", "invalid-operation"),
        ("NullReferenceException", "null-reference"),
        ("JSException", "js-interop"),
        ("JsonException", "json"),
        ("ObjectDisposedException", "disposed"),
        ("NotSupportedException", "not-supported"),
        ("ArgumentException", "argument"),
        ("TypeError", "js-type"),
        ("Could not find", "missing-js-member"),
        ("Cannot provide a value for property", "missing-component-service"),
        ("No service for type", "missing-service"),
        ("Unable to set property", "component-parameter"),
        ("Cannot wait on monitors", "browser-sync-wait"),
        ("synchronous", "synchronous-call"),
        ("Value cannot be null", "null-argument"),
        ("Object reference not set", "null-reference"),
        ("Nullable object must have a value", "null-value"),
        ("A task was canceled", "task-canceled"),
        ("TaskCanceledException", "task-canceled"),
        ("Unable to cast", "cast"),
        ("MONO_WASM", "wasm-runtime"),
        ("CSS", "css"),
        ("Unhandled exception rendering component", "component-render")
    ];

    private static readonly string[] KnownComponents =
    [
        "ManagerHeader", "ManagerShellLayout", "NavMenu", "CultureSwitcher",
        "ExternalMembers", "ExternalIdentityAuthorityForm", "ManagerAssistantDrawer",
        "ManagerThemeService", "AppProvider", "NeoUI", "Radzen"
    ];

    private static readonly string[] FrameworkMarkers =
    [
        "parameter", "property", "cascading", "Body", "AuthenticationState",
        "dispatcher", "current thread", "disposed", "canceled", "cyclic", "circular",
        "duplicate", "already", "subscriber", "section", "service", "not found",
        "inherit", "implements", "serialization", "deserialization", "scope",
        "prerender", "unsupported", "localStorage", "required", "nullable",
        "null", "empty", "marked", "synchronization"
    ];

    private static string FrameworkHint(string text) => string.Join(",", FrameworkMarkers
        .Where(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)));

    private static string ComponentHint(string text)
    {
        var matches = KnownComponents.Where(component => text.Contains(component, StringComparison.Ordinal));
        var hint = string.Join(",", matches);
        return hint.Length == 0 ? "unavailable" : hint;
    }

    private static string ClassifyConsoleError(string text)
    {
        foreach (var (marker, category) in ConsoleErrorMarkers)
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase)) return category;
        return "other-console-error";
    }

    private static string SafeLocation(string? value, Uri origin)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var location)) return "unavailable";
        return location.Scheme == origin.Scheme && location.Host == origin.Host && location.Port == origin.Port
            ? location.AbsolutePath
            : "foreign-origin";
    }

    private static string ClassifyRequestFailure(string? failure)
    {
        if (failure?.Contains("ERR_ABORTED", StringComparison.OrdinalIgnoreCase) == true) return "aborted";
        if (failure?.Contains("ERR_CONNECTION_RESET", StringComparison.OrdinalIgnoreCase) == true) return "connection-reset";
        if (failure?.Contains("ERR_CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase) == true) return "connection-refused";
        if (failure?.Contains("ERR_TIMED_OUT", StringComparison.OrdinalIgnoreCase) == true) return "timed-out";
        if (failure?.Contains("ERR_FAILED", StringComparison.OrdinalIgnoreCase) == true) return "failed";
        return "unknown";
    }

    private sealed class SyntheticManagerHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        public Uri BaseUri { get; }
        public TaskCompletionSource<SiteSelectionCompletion> SiteSelectionCompletion { get; }
        private SyntheticManagerHost(WebApplication app, Uri baseUri, TaskCompletionSource<SiteSelectionCompletion> completion)
            => (this.app, BaseUri, SiteSelectionCompletion) = (app, baseUri, completion);

        public static async Task<SyntheticManagerHost> StartAsync(IExternalIdentityManagerScopeResolver scope, IExternalIdentityAuthorityService authority)
        {
            var baseUri = new Uri($"http://127.0.0.1:{AllocatePort()}/");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(SameOriginExternalIdentityAdminWasmTests).Assembly.GetName().Name,
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseUrls(baseUri.ToString());
            builder.WebHost.UseStaticWebAssets();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents().AddAuthenticationStateSerialization();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddSingleton<Aero.Cms.Contracts.Services.AppState>();
            builder.Services.AddAuthentication(SyntheticCookieHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, SyntheticCookieHandler>(SyntheticCookieHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization(options => options.AddPolicy("AeroAdmin", policy => policy.RequireRole("Admin")));
            builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
            builder.Services.AddScoped(_ => scope);
            builder.Services.AddScoped(_ => authority);

            var app = builder.Build();
            var siteSelectionCompletion = new TaskCompletionSource<SiteSelectionCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            // The component's initial read is intentionally a fixed safe state:
            // map the native token and PUT endpoints below, but avoid any store.
            app.Use(async (context, next) =>
            {
                if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/api/v1/admin/external-identity/authority")
                {
                    if (!(context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Admin")))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }
                    await Results.Json(new ExternalIdentityAuthorityState(false, null, null, null, null, null, false)).ExecuteAsync(context);
                    return;
                }
                await next(context);
            });
            app.MapGet("/__e2e/sign-in", (HttpContext context) =>
            {
                context.Response.Cookies.Append(SyntheticCookieHandler.CookieName, "1", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
                return Results.Redirect("/manager/external-members");
            }).AllowAnonymous();
            // Manager-shell dependencies are browser-safe fixed fixtures, not CMS reads.
            app.MapGet("/api/v1/admin/auth/me", () => Results.Json(new
            {
                userId = 1L,
                userName = "manager",
                email = "manager@example.test",
                roles = new[] { "Admin" },
                isAdmin = true,
                nickname = "manager",
                permissions = Array.Empty<string>()
            })).RequireAuthorization("AeroAdmin");
            app.MapGet("/api/v1/admin/docs/categories", () => Results.Json(Array.Empty<object>())).RequireAuthorization("AeroAdmin");
            var site = new { id = 702L, name = "E2E site", tenantId = 701L, primaryHost = baseUri.Host,
                isEnabled = true, defaultCulture = "en-US", supportedCultures = new[] { "en-US" } };
            app.MapGet("/api/v1/admin/sites/current", () => Results.Json(site)).RequireAuthorization("AeroAdmin");
            app.MapPost("/api/v1/admin/sites/current", async (HttpRequest request, HttpResponse response, CancellationToken cancellationToken) =>
            {
                var selected = await request.ReadFromJsonAsync<SetCurrentSiteRequest>(cancellationToken: cancellationToken);
                if (selected is not { SiteId: 702L })
                    return Results.BadRequest();

                response.Headers.CacheControl = "no-store";
                response.OnCompleted(() =>
                {
                    siteSelectionCompletion.TrySetResult(new(selected.SiteId, response.StatusCode,
                        response.HasStarted, request.HttpContext.RequestAborted.IsCancellationRequested));
                    return Task.CompletedTask;
                });
                return Results.Ok();
            }).RequireAuthorization("AeroAdmin").DisableAntiforgery();
            app.MapGet("/api/v1/admin/sites/702", () => Results.Json(site)).RequireAuthorization("AeroAdmin");
            app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, $"{typeof(SameOriginExternalIdentityAdminWasmTests).Assembly.GetName().Name}.staticwebassets.endpoints.json"));
            app.MapRazorComponents<Aero.Cms.UI.App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Aero.Cms.Shared.Pages.Manager.ExternalMembers.ExternalMembers).Assembly);
            app.MapExternalIdentityAdminApi();
            await app.StartAsync();
            return new(app, baseUri, siteSelectionCompletion);
        }

        public async ValueTask DisposeAsync() => await app.DisposeAsync();
        private static int AllocatePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }

    private sealed class SyntheticCookieHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "e2e-cookie";
        public const string CookieName = "e2e-manager";
        public SyntheticCookieHandler(Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Cookies.ContainsKey(CookieName)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Name, "e2e-manager"), new Claim(ClaimTypes.Role, "Admin") }, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
