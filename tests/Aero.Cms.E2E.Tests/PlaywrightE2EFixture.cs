using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Hosting.Defaults;
using Aero.Cms.Web.Bootstrap;
using Aero.Cms.Web.Core.Eextensions;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MysticMind.PostgresEmbed;
using Npgsql;
using Scalar.AspNetCore;
using Serilog;
using System.Globalization;
using System.Net.Sockets;
using IPAddress = System.Net.IPAddress;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using SurrealDb.Embedded.InMemory;
using Hydro.Configuration;

namespace Aero.Cms.E2E.Tests;

/// <summary>
/// Owns the single browser/Kestrel/Postgres fixture shared by the test session.
/// Multiple session hooks may initialize or dispose it; both operations are idempotent.
/// </summary>
public static class SharedPlaywrightE2EFixture
{
    public static PlaywrightE2EFixture Instance { get; } = new();
}

public sealed record LocalizationContentEntrySeed(long ItemId, long AiBlockedItemId, string Alias, string Slug, string ViewAlias);

/// <summary>
/// E2E test fixture that starts a real Kestrel-hosted WebApplication (not Alba/TestServer).
///
/// Why not Alba? Alba uses <c>TestServer</c> internally, which handles requests in-memory
/// via a special HttpClient. Playwright (real browser) needs a real TCP listener.
///
/// Startup sequence:
///   1. Start embedded Postgres via MysticMind.PostgresEmbed
///   2. Build a WebApplication using the public arbitrary-host registration contract
///   3. Configure the public staged middleware pipeline (minus HTTPS)
///   4. Start the app via <c>app.StartAsync()</c> on a background thread
///   5. Poll for HTTP readiness
///   6. Start Playwright
///   7. On dispose: stop the app and clean up all resources
/// </summary>
public sealed class PlaywrightE2EFixture : IAsyncDisposable
{
    private const string E2EAuthenticationScheme = "E2E";
    private static readonly long E2EUserId = Snowflake.NewId();
    private static readonly SemaphoreSlim Lock = new(1, 1);
    // Session-scoped E2E classes share one browser page. TUnit scheduling may still
    // overlap individual tests, so serialize a complete navigation journey here.
    private static readonly SemaphoreSlim BrowserJourneyLock = new(1, 1);

    private PgServer? _postgres;
    private int _pgPort;
    private AeroDB.Sable.IDocumentStore? _store;
    private WebApplication? _app;
    private CancellationTokenSource? _appCts;
    private Task? _appTask;
    private int _disposeState;

    public string BaseUrl { get; private set; } = "http://localhost:5555";
    public IPage? Page { get; private set; }
    public IBrowser? Browser { get; private set; }
    public IBrowserContext? BrowserContext { get; private set; }
    public IPlaywright? PlaywrightInstance { get; private set; }
    public long HomePageId { get; private set; }
    public long BlockPageId { get; private set; }
    public long SiteId { get; private set; }
    public long TenantId { get; private set; }

    public async Task InitializeAsync()
    {
        await Lock.WaitAsync();
        try
        {
            if (_app is not null) return;

            // Several E2E classes have independent session fixtures. Give each
            // real Kestrel host its own loopback endpoint rather than competing
            // for the historical fixed port during a focused test run.
            BaseUrl = AllocateLoopbackBaseUrl();

            // ── 1. Start embedded Postgres ──────────────────────────────
            const string dbName = "aero_e2e_test";
            _pgPort = 5436;
            _postgres = new PgServer("18.3.0", dbName, port: _pgPort, clearInstanceDirOnStop: true);
            await _postgres.StartAsync();
            await EnsureDatabaseAsync(dbName, _pgPort);

            var connString = $"Host=localhost;Port={_pgPort};Username={dbName};Database={dbName};";

            // ── 3. Build & start the real web app with Kestrel ───────────
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = Directory.GetCurrentDirectory(),
                EnvironmentName = Environments.Development,
                // MapStaticAssets resolves the generated endpoint manifest from
                // the host application name. TUnit's process entry point is not
                // this test assembly, so pin the host identity to the executable
                // that owns Aero.Cms.E2E.Tests.staticwebassets.endpoints.json.
                ApplicationName = typeof(PlaywrightE2EFixture).Assembly.GetName().Name
            });

            // This executable is the real Kestrel host for the browser test,
            // rather than the standalone Aero.Cms.Web host. Load its generated
            // static-web-assets manifest before MapAeroCms maps the endpoints so
            // the WebAssembly bootstrap script is available to the browser.
            builder.WebHost.UseStaticWebAssets();

            // Override config for testing
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:aero"] = connString,
                ["AeroCms:Bootstrap:State"] = "Running",
                ["AeroCms:Bootstrap:SetupComplete"] = "true",
                ["AeroCms:Bootstrap:SeedComplete"] = "true",
                ["AeroCms:Infrastructure:DatabaseMode"] = "Embedded",
                ["AeroCms:Bootstrap:HasBootstrapConfig"] = "true",
                ["AeroCms:Bootstrap:RequestedManagerAuthenticationProvider"] = "local",
                ["AeroCms:Bootstrap:RequestedMemberAuthenticationProvider"] = "disabled",
                ["AeroCms:Infrastructure:CacheMode"] = "Local",
                ["AeroCms:Infrastructure:SecretProvider"] = "Local Certificate",
                ["Aero:Embedded:Port"] = _pgPort.ToString(),
                ["Aero:Embedded:Username"] = dbName,
                ["Aero:Embedded:Password"] = "",
                ["Aero:Embedded:Database"] = dbName,
                ["urls"] = BaseUrl,
                ["ApiSettings:BaseUrl"] = BaseUrl,
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",

                // Service discovery: typed HTTP clients resolve "localhost" to the
                // default https://localhost:333. Override to point at our test port.
                ["Services:localhost:Default:0"] = BaseUrl,
                ["Services:localhost:Https:0"] = BaseUrl
            });

            // Configure Serilog for bootstrap logging
            AeroCmsExtensions.ConfigureAeroCmsBootstrapLogging(builder.Environment.ContentRootPath);

            await builder
                .AddAeroCms(AeroCmsDefaultCatalog.Catalog, options =>
                    options.ConfigureApplicationCookie = cookie =>
                        cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest)
                .WithSetupSettingsDirectory(builder.Environment.ContentRootPath)
                .RegisterHostAsync<PlaywrightE2EFixture>();

            // Query-backed views intentionally require an explicitly supplied
            // read-only connection.  The browser fixture supplies a deterministic
            // implementation and a generic compile-time shape; production hosts
            // must still opt in with their own separately constrained identity.
            builder.Services.AddSingleton<IContentShape, E2ESurrealViewShape>();
            builder.Services.AddSingleton<IContentViewSource, E2ESurrealViewSource>();
            builder.Services.RemoveAll<IReadOnlyContentViewExecutor>();
            builder.Services.AddSingleton<IReadOnlyContentViewExecutor, E2EReadOnlyContentViewExecutor>();
            builder.Services.RemoveAll<IAdminReadOnlyContentViewExecutor>();
            builder.Services.AddSingleton<IAdminReadOnlyContentViewExecutor, E2EReadOnlyContentViewExecutor>();

            builder.Services.RemoveAll<AeroDB.Sable.IDocumentStore>();
            builder.Services.AddAeroDB(options =>
            {
                options.Namespace = "aero_e2e";
                options.Database = "aero_e2e";
                options.ClientFactory = () => new SurrealDbMemoryClient();
                options.Schema.For<AeroRole>().Identity(role => role.Id);
                options.Schema.For<AeroUser>().Identity(user => user.Id);
                options.Events.StreamIdentity = StreamIdentity.AsString;
            });

            builder.Services.RemoveAll<IBootstrapStateProvider>();
            builder.Services.AddSingleton<IBootstrapStateProvider>(
                new AppSettingsBootstrapStateProvider(builder.Configuration));

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = E2EAuthenticationScheme;
                    options.DefaultChallengeScheme = E2EAuthenticationScheme;
                    options.DefaultForbidScheme = E2EAuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, E2EAuthenticationHandler>(
                    E2EAuthenticationScheme,
                    _ => { });

            // Production manager endpoints intentionally pin their policies to the
            // manager routing scheme. Replace only those manager policies in this
            // in-process test host so every browser and typed-client request uses the
            // deterministic E2E principal instead of requiring a real login cookie.
            builder.Services.PostConfigure<AuthorizationOptions>(authorization =>
            {
                var managerPolicy = new AuthorizationPolicyBuilder(E2EAuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
                authorization.DefaultPolicy = managerPolicy;
                authorization.AddPolicy(ManagerRecoveryDefaults.ManagerPolicy, managerPolicy);
                authorization.AddPolicy(
                    "AeroAdmin",
                    new AuthorizationPolicyBuilder(E2EAuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireRole(CmsRoleNames.Admin)
                        .Build());
            });

            // Remove the embedded DB background service (already have PG running externally)
            var embeddedDbDescriptor = builder.Services.FirstOrDefault(d =>
                d.ImplementationType?.Name == "AeroEmbeddedDbService");
            if (embeddedDbDescriptor is not null)
                builder.Services.Remove(embeddedDbDescriptor);

            _app = builder.Build();

            // The E2E host replaces the production embedded Sable store with an
            // in-memory Sable store and therefore removes AeroEmbeddedDbService.
            // Publish the equivalent readiness state explicitly so runtime startup
            // does not wait for a hosted service that this fixture intentionally
            // removed.
            var readiness = _app.Services.GetRequiredService<IInfrastructureReadinessSnapshot>();
            readiness.AeroDbReady = true;
            _app.Services.GetRequiredService<IMultiStartupSignal>()
                .MarkReady(StartupServiceNames.AeroDb);

            // Configure middleware pipeline (mirrors RunAeroCmsAsync without HTTPS)
            ConfigureTestMiddleware(_app);

            // Start the app on a background thread
            _appCts = new CancellationTokenSource();
            _appTask = Task.Run(async () =>
            {
                try
                {
                    await _app.StartAsync(_appCts.Token);

                    // Wait until cancellation is requested (by DisposeAsync)
                    var tcs = new TaskCompletionSource();
                    _appCts.Token.Register(() => tcs.TrySetResult());
                    await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                }
                finally
                {
                    try { await _app.StopAsync(); }
                    catch { /* best-effort */ }
                }
            }, _appCts.Token);

            // Wait for the app to start responding
            // A cold Aero host starts Orleans, Garnet, embedded persistence, and the
            // complete module graph. Twenty seconds is not a reliable budget on a
            // clean CI or developer machine, even though subsequent runs are faster.
            await WaitForAppReadyAsync();

            // ── 4. Seed database (roles, admin user, site, homepage) ────
            await SeedDatabaseAfterStartAsync();

            // ── 5. Start Playwright ─────────────────────────────────────
            PlaywrightInstance = await Playwright.CreateAsync();
            Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--ignore-certificate-errors" }
            });
            BrowserContext = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });
            await BrowserContext.AddCookiesAsync(
            [
                new Cookie
                {
                    Name = "AeroCms.SiteId",
                    Value = SiteId.ToString(CultureInfo.InvariantCulture),
                    Url = BaseUrl,
                    SameSite = SameSiteAttribute.Lax
                }
            ]);
            Page = await BrowserContext.NewPageAsync();
            Page.Console += (_, message) =>
            {
                if (message.Type is "error" or "warning")
                    Console.WriteLine("[Browser console:{0}] {1}", message.Type, message.Text);
            };
            Page.PageError += (_, error) =>
                Console.WriteLine("[Browser page error] {0}", error);
            Page.RequestFailed += (_, request) =>
                Console.WriteLine("[Browser request failed] {0} {1}", request.Url, request.Failure);
            Page.Request += (_, request) =>
            {
                if (request.Url.Contains("/api/v1/admin/content-views/", StringComparison.Ordinal))
                    Console.WriteLine("[Browser content-view cookie] {0}", request.Headers.TryGetValue("cookie", out var cookie) ? cookie : "<none>");
            };
            Page.Response += async (_, response) =>
            {
                if (response.Status >= StatusCodes.Status400BadRequest)
                {
                    Console.WriteLine("[Browser response:{0}] {1}", response.Status, response.Url);
                    if (response.Url.Contains("/api/v1/admin/content-views/", StringComparison.Ordinal))
                    {
                        try
                        {
                            Console.WriteLine("[Browser response body] {0}", await response.TextAsync());
                        }
                        catch (PlaywrightException)
                        {
                            // A failed/aborted browser request may not expose a response body.
                        }
                    }
                }
            };
        }
        finally
        {
            Lock.Release();
        }
    }

    // ── Middleware pipeline ──────────────────────────────────────────────

    /// <summary>
    /// Configures the ASP.NET Core middleware pipeline for testing.
    /// Mirrors <c>AeroCmsExtensions.RunAeroCmsAsync</c> but skips:
    ///   • HTTPS redirection — Playwright uses plain HTTP on localhost
    ///   • HSTS — not needed for ephemeral test server
    ///   • Bootstrap-failure marking — unnecessary for tests
    ///   • Blocking WaitForShutdownAsync — lifecycle is managed externally
    /// </summary>
    private void ConfigureTestMiddleware(WebApplication app)
    {
        app.Map("/__e2e/ready", readyApp =>
            readyApp.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            }));

        // The E2E authentication handler supplies the request principal without
        // persisting Identity records. Mirror that principal for the endpoint
        // consumed by the Blazor authentication-state provider.
        app.Map("/api/v1/admin/auth/me", authApp =>
            authApp.Run(context => context.Response.WriteAsJsonAsync(new
            {
                userId = E2EUserId,
                userName = "admin@aero.local",
                email = "admin@aero.local",
                roles = new[] { CmsRoleNames.Admin },
                isAdmin = true,
                nickname = "E2E Administrator",
                permissions = new[] { "create", "read", "update", "delete" }
            })));

        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        app.UseStatusCodePagesWithReExecute("/oops", "?status={0}");
        app.UseAeroCmsRouting();
        app.UseStaticFiles();
        app.UseAeroCmsSiteAndLocalization();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseAeroCmsRequestPipeline();
        app.UseAntiforgery();

        // The test runner's bootstrap entry point does not automatically expose
        // this executable's endpoint manifest to MapStaticAssets. Map the
        // generated manifest explicitly before AeroCMS adds its interactive
        // WebAssembly render mode.
        app.MapStaticAssets(Path.Combine(
            AppContext.BaseDirectory,
            $"{typeof(PlaywrightE2EFixture).Assembly.GetName().Name}.staticwebassets.endpoints.json"));
        app.MapAeroCms();
        app.UseAeroCmsTerminalPipeline();
    }

    // ── Readiness polling ────────────────────────────────────────────────

    /// <summary>
    /// Polls the app URL until it returns a response (success or redirect),
    /// confirming the Kestrel server is ready to serve requests.
    /// </summary>
    private async Task WaitForAppReadyAsync(int maxRetries = 60)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < maxRetries; i++)
        {
            if (_appTask?.IsFaulted == true)
            {
                await _appTask;
            }

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/__e2e/ready");
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect)
                    return;
            }
            catch
            {
                // Server not ready yet
            }
            await Task.Delay(1000);
        }

        var taskState = _appTask?.Status.ToString() ?? "not created";
        throw new TimeoutException(
            $"App did not start within {maxRetries} seconds at {BaseUrl}. Host task state: {taskState}.");
    }

    // ── Login helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the browser context is authenticated. The E2E host uses a test
    /// authentication scheme, while the form fallback keeps the fixture usable
    /// with a normally configured host.
    /// </summary>
    public async Task LoginAsync()
    {
        if (Page is null) throw new InvalidOperationException("Page not initialized");

        var authenticatedResponse = await Page.APIRequest.GetAsync(
            $"{BaseUrl}/api/v1/admin/auth/me");
        if (authenticatedResponse.Status == StatusCodes.Status200OK)
        {
            // Playwright's API request context and the browser circuit can start
            // independently. Establish the selected-site cookie through the
            // product endpoint so the WebAssembly client receives the same
            // authorized site scope as a real manager session.
            var selectionResponse = await Page.APIRequest.PostAsync(
                $"{BaseUrl}/api/v1/admin/sites/current",
                new APIRequestContextOptions
                {
                    DataObject = new { siteId = SiteId }
                });
            if (selectionResponse.Status != StatusCodes.Status200OK)
                throw new InvalidOperationException($"Failed to select E2E site: HTTP {selectionResponse.Status}.");

            // APIRequest owns a separate cookie jar. Mirror the server-accepted
            // selection into the real browser context that hosts the WASM app.
            await BrowserContext!.AddCookiesAsync(
            [
                new Cookie
                {
                    Name = "AeroCms.SiteId",
                    Value = SiteId.ToString(CultureInfo.InvariantCulture),
                    Url = BaseUrl,
                    SameSite = SameSiteAttribute.Lax
                }
            ]);
            var selectedCookies = await BrowserContext.CookiesAsync([BaseUrl]);
            var selectedSiteCookie = selectedCookies.SingleOrDefault(cookie => cookie.Name == "AeroCms.SiteId");
            Console.WriteLine(
                "[Login] Browser site cookie present: {0}; domain={1}; path={2}",
                selectedSiteCookie?.Value == SiteId.ToString(CultureInfo.InvariantCulture),
                selectedSiteCookie?.Domain ?? "<none>",
                selectedSiteCookie?.Path ?? "<none>");

            Console.WriteLine("[Login] E2E authentication scheme is active");
            return;
        }

        // Navigate to login page
        await Page.GotoAsync($"{BaseUrl}/manager/login", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Check if already logged in (redirected away from login page)
        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/manager/login", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[Login] Already authenticated (redirected to {0})", currentUrl);
            return;
        }

        // InputText does not emit a type attribute for the username field. Scope
        // selectors to the named form so hydration and unrelated inputs cannot
        // change which controls the fixture targets.
        var loginForm = Page.Locator("form[method='post'][formname='login'], form[name='login'], form").First;
        await loginForm.WaitForAsync(new() { Timeout = 15000, State = WaitForSelectorState.Visible });

        var loginInputs = loginForm.Locator("input:not([type='hidden'])");
        await loginInputs.First.WaitForAsync(new() { Timeout = 10000, State = WaitForSelectorState.Visible });

        await loginInputs.Nth(0).FillAsync("admin@aero.local");
        await loginForm.Locator("input[type='password']").FillAsync("Admin123!");
        await loginForm.Locator("button[type='submit']").ClickAsync();

        // Wait for redirect to /manager (successful login navigates to /manager)
        try
        {
            await Page.WaitForURLAsync("**/manager**", new() { Timeout = 15000 });
            Console.WriteLine("[Login] Browser-based login complete");
        }
        catch (TimeoutException)
        {
            // Login might have failed - check for error messages
            var errorElement = await Page.QuerySelectorAsync(".validation-message, .alert-danger, .text-danger");
            var errorText = errorElement is not null ? await errorElement.TextContentAsync() : "no error found";
            throw new InvalidOperationException($"Login failed. Error: {errorText}");
        }
    }

    public async Task RunBrowserJourneyAsync(Func<Task> journey)
    {
        await BrowserJourneyLock.WaitAsync();
        try
        {
            await journey();
        }
        finally
        {
            BrowserJourneyLock.Release();
        }
    }

    /// <summary>
    /// Navigates to the login page and waits for Blazor WASM to download/cache.
    /// Call this once before any browser-based page navigation tests.
    /// The first call takes ~15-20s (WASM download); subsequent navigations use the cache.
    /// </summary>
    private bool _warmedUp;

    public async Task WarmUpBlazorAsync(int timeoutMs = 60000)
    {
        if (_warmedUp) return;
        _warmedUp = true;

        if (Page is null) throw new InvalidOperationException("Page not initialized. Call InitializeAsync first.");

        // Ensure authenticated before warming up Blazor
        await LoginAsync();

        // Navigate to the pages grid to establish the SignalR circuit
        await Page.GotoAsync($"{BaseUrl}/manager/pages", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = timeoutMs
        });

        // Wait for a known page element to confirm circuit is active
        try
        {
            await Page.WaitForSelectorAsync("a[href*='manager']", new() { Timeout = 30000, State = WaitForSelectorState.Attached });
            Console.WriteLine("[Warmup] Blazor circuit warmup complete");
        }
        catch
        {
            // Fallback: wait fixed time and hope
            Console.WriteLine("[Warmup] Blazor warmup fallback — waiting 10s");
            await Task.Delay(10000);
        }
    }

    private static Cookie? ParseSetCookie(string setCookie)
    {
        var parts = setCookie.Split(';', 2);
        if (parts.Length == 0) return null;

        var kv = parts[0].Trim().Split('=', 2);
        if (kv.Length != 2) return null;

        var cookie = new Cookie
        {
            Name = kv[0].Trim(),
            Value = kv[1].Trim(),
            Domain = "localhost",
            Path = "/",
            HttpOnly = false, // Override HttpOnly so Playwright can store it
            Secure = false
        };

        // Parse attributes from the rest
        if (parts.Length > 1)
        {
            var attrs = parts[1].ToLowerInvariant();
            if (attrs.Contains("secure")) cookie.Secure = true;
            if (attrs.Contains("httponly")) cookie.HttpOnly = true;
            if (attrs.Contains("samesite=lax")) cookie.SameSite = SameSiteAttribute.Lax;
            if (attrs.Contains("samesite=strict")) cookie.SameSite = SameSiteAttribute.Strict;
            if (attrs.Contains("samesite=none")) cookie.SameSite = SameSiteAttribute.None;

            var pathMatch = System.Text.RegularExpressions.Regex.Match(parts[1], @"path=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (pathMatch.Success) cookie.Path = pathMatch.Groups[1].Value.Trim();
        }

        return cookie;
    }

    // ── Seed / DB helpers ────────────────────────────────────────────────

    /// <summary>
    /// Seeds the database with roles, an admin user, a default site, and a
    /// homepage.  Runs after the Kestrel server is accepting requests so that
    /// the full DI container (including <c>UserManager</c>, <c>RoleManager</c>,
    /// and the app-configured Sable <c>IDocumentStore</c>) is available.
    ///
    /// The configuration keys <c>SetupComplete</c> / <c>SeedComplete</c> are set
    /// to <c>true</c> so the app does <b>not</b> redirect to the setup wizard;
    /// this method performs the equivalent work programmatically.
    /// </summary>
    private async Task SeedDatabaseAfterStartAsync()
    {
        using var scope = _app!.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var documentStore = sp.GetRequiredService<AeroDB.Sable.IDocumentStore>();
        _store = documentStore;

        // ── Site (tenant) ───────────────────────────────────────────────
        await using var session = await documentStore.LightweightSessionAsync();

            var site = (await session.Query<SitesModel>().FirstOrDefaultAsync(s => s.Name == "Aero CMS"))!;
        if (site is null)
        {
            site = new SitesModel
            {
                Id                 = Snowflake.NewId(),
                Name               = "Aero CMS",
                Description        = "Default E2E test site",
                IsEnabled          = true,
                DefaultCulture     = "en-US",
                SupportedCultures  = ["en-US", "ar-SA"],
                TenantId           = Snowflake.NewId()
            };
            session.Store(site);
        }
        SiteId = site.Id;
        TenantId = site.TenantId;

        // ── Site host ───────────────────────────────────────────────────
        var hostEntry = await session.Query<SiteHost>()
            .FirstOrDefaultAsync(h => h.Host == "localhost" && h.SiteId == site.Id);
        if (hostEntry is null)
        {
            session.Store(new SiteHost
            {
                Id        = Snowflake.NewId(),
                SiteId    = site.Id,
                Host      = "localhost",
                IsPrimary = true
            });
        }

        // ── User → site assignment ──────────────────────────────────────
        var assignment = await session.Query<UserSiteAssignment>()
            .FirstOrDefaultAsync(a => a.UserId == E2EUserId && a.SiteId == site.Id);
        if (assignment is null)
        {
            session.Store(new UserSiteAssignment
            {
                Id          = Snowflake.NewId(),
                UserId      = E2EUserId,
                SiteId      = site.Id,
                Permissions = ["create", "read", "update", "delete"]
            });
        }

        session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DatabaseMode = "Embedded",
            CacheMode = "Local",
            SecretProvider = "Local Certificate",
            AdminEmail = "admin@aero.local",
            SiteName = site.Name ?? "Aero CMS",
            HomepageTitle = "Home",
            BlogName = "E2E Blog",
            CreatedTenantId = site.TenantId,
            CreatedSiteId = site.Id,
            Hostname = "localhost",
            DefaultCulture = site.DefaultCulture,
            SupportedCultures = site.SupportedCultures ?? []
        });

        // ── Home page ───────────────────────────────────────────────────
        var homePage = await session.Query<PageDocument>()
            .FirstOrDefaultAsync(p => p.SiteId == site.Id && p.Slug == "/");
        if (homePage is null)
        {
            var page = new PageDocument
            {
                Id                = Snowflake.NewId(),
                SiteId            = site.Id,
                Slug              = "/",
                Title             = "Home",
                Summary           = "Welcome to Aero CMS",
                Path              = "/",
                Depth             = 0,
                Order             = 0,
                Kind              = PageKind.Homepage,
                PublicationState  = ContentPublicationState.Published,
                PublishedOn       = DateTimeOffset.UtcNow,
                ShowInNavMenu     = false,
                ShowHeaderNavigation = true
            };
            page.ReplaceDraftContent(new HtmlPageContent(), DateTimeOffset.UtcNow);
            page.PublishDraftContent(DateTimeOffset.UtcNow);
            session.Store(page);
            homePage = page;
        }
        HomePageId = homePage.Id;

        // ── Test page with hero block ────────────────────────────────────
        var blockPage = await session.Query<PageDocument>()
            .FirstOrDefaultAsync(p => p.SiteId == site.Id && p.Slug == "test-blocks-page");
        if (blockPage is null)
        {
            blockPage = new PageDocument
            {
                Id                = Snowflake.NewId(),
                SiteId            = site.Id,
                Slug              = "test-blocks-page",
                Title             = "Test Blocks Page",
                Summary           = "E2E page with seeded blocks",
                Path              = "/test-blocks-page",
                Depth             = 0,
                Order             = 1,
                Kind              = PageKind.Standard,
                PublicationState  = ContentPublicationState.Published,
                PublishedOn       = DateTimeOffset.UtcNow,
                ShowInNavMenu     = false,
                ShowHeaderNavigation = false
            };
            blockPage.ReplaceDraftContent(
                CreateTestPageContent(
                    "Seeded Hero Block",
                    "This hero was seeded by the E2E test fixture"),
                DateTimeOffset.UtcNow);
            blockPage.PublishDraftContent(DateTimeOffset.UtcNow);
            session.Store(blockPage);
        }
        BlockPageId = blockPage.Id;

        await session.SaveChangesAsync();

        await using (var resolutionScope = _app.Services.CreateAsyncScope())
        {
            var siteLookup = resolutionScope.ServiceProvider.GetRequiredService<ISiteLookupService>();
            var resolvedSite = await siteLookup.ResolveByHostAsync("localhost");
            if (resolvedSite?.Id != site.Id)
            {
                throw new InvalidOperationException(
                    $"E2E site host resolution failed. Expected site {site.Id} for localhost, " +
                    $"but resolved {resolvedSite?.Id.ToString() ?? "no site"}.");
            }
        }

        await using var verification = await documentStore.QuerySessionAsync();
        var allPages = await verification.Query<PageDocument>().ToListAsync();
        var sitePages = await verification.Query<PageDocument>()
            .Where(page => page.SiteId == site.Id)
            .ToListAsync();
        var activeSitePages = await verification.Query<PageDocument>()
            .Where(page => page.SiteId == site.Id && page.Deleted == false)
            .ToListAsync();

        Console.WriteLine(
            "[Fixture] Seed verification: {0} total pages, {1} site pages, {2} active site pages for site {3}",
            allPages.Count,
            sitePages.Count,
            activeSitePages.Count,
            site.Id);

        if (sitePages.Count < 2)
        {
            throw new InvalidOperationException(
                $"E2E page seeding did not round-trip for site {site.Id}. " +
                $"Expected at least 2 pages but found {sitePages.Count}.");
        }
    }

    /// <summary>
    /// Resets the seeded block page back to its original single "Seeded Hero Block".
    /// Call before any mutating test (duplicate, delete, etc.) to ensure a known state.
    /// </summary>
    public async Task ResetBlockPageAsync()
    {
        await using var session = await _store!.LightweightSessionAsync();
        var page = await session.LoadAsync<PageDocument>(BlockPageId);
        if (page is null) return;

        page.ReplaceDraftContent(
            CreateTestPageContent(
                "Seeded Hero Block",
                "This hero was seeded by the E2E test fixture"),
            DateTimeOffset.UtcNow);
        page.PublishDraftContent(DateTimeOffset.UtcNow);

        session.Store(page);
        await session.SaveChangesAsync();
        Console.WriteLine("[Fixture] Block page reset to original seeded state");
    }

    /// <summary>
    /// Resets the home page back to an empty blocks list.
    /// Call before tests that add blocks to the home page (e.g., media image tests).
    /// </summary>
    public async Task ResetHomePageAsync()
    {
        await using var session = await _store!.LightweightSessionAsync();
        var page = await session.LoadAsync<PageDocument>(HomePageId);
        if (page is null) return;
        page.ReplaceDraftContent(new HtmlPageContent(), DateTimeOffset.UtcNow);
        page.PublishDraftContent(DateTimeOffset.UtcNow);
        session.Store(page);
        await session.SaveChangesAsync();
        Console.WriteLine("[Fixture] Home page reset to empty state");
    }

    /// <summary>
    /// Creates an isolated blank draft for editor acceptance tests that exercise
    /// composition, persistence, preview, and publication rather than page creation.
    /// </summary>
    public async Task<long> CreateBlankDraftPageAsync(string title, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = slug.Trim().Trim('/');
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = SiteId,
            Slug = normalizedSlug,
            Title = title.Trim(),
            Path = $"/{normalizedSlug}",
            Depth = 0,
            Order = 100,
            Kind = PageKind.Standard,
            PublicationState = ContentPublicationState.Draft,
            ShowInNavMenu = false,
            ShowHeaderNavigation = false
        };
        page.ReplaceDraftContent(new HtmlPageContent(), DateTimeOffset.UtcNow);

        await using var session = await _store!.LightweightSessionAsync();
        session.Store(page);
        await session.SaveChangesAsync();
        return page.Id;
    }

    /// <summary>
    /// Seeds one disposable structured-content palette fixture for PageEditor browser tests.
    /// </summary>
    public async Task SeedContentPaletteAsync(string alias, int itemCount)
    {
        if (_app is null)
        {
            throw new InvalidOperationException("The E2E application has not been initialized.");
        }

        await using var scope = _app.Services.CreateAsyncScope();
        var contentTypes = scope.ServiceProvider.GetRequiredService<IContentTypeService>();
        var contentItems = scope.ServiceProvider.GetRequiredService<IContentService>();
        var typeResult = await contentTypes.SaveAsync(new ContentTypeDefinition
        {
            Id = 0,
            SiteId = SiteId,
            Alias = alias,
            Name = "Page Editor Articles",
            Description = "Disposable typed-content PageEditor fixture",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "headline",
                    Label = "Headline",
                    FieldType = "text",
                    Required = true
                },
                new ContentFieldDefinition
                {
                    Name = "score",
                    Label = "Score",
                    FieldType = "number"
                }
            ]
        });
        if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure typeFailure)
        {
            throw new InvalidOperationException(typeFailure.Error.ToString());
        }

        for (var index = 1; index <= itemCount; index++)
        {
            var itemResult = await contentItems.SaveAsync(new ContentItem
            {
                Id = 0,
                SiteId = SiteId,
                ContentTypeAlias = alias,
                Title = $"Page Editor Item {index:00}",
                Slug = $"page-editor-item-{index:00}",
                Culture = "en-US",
                VersionNumber = 1,
                Fields = new Dictionary<string, JsonElement>
                {
                    ["headline"] = JsonSerializer.SerializeToElement($"Headline {index:00}"),
                    ["score"] = JsonSerializer.SerializeToElement(index)
                }
            });
            if (itemResult is Result<ContentItem, AeroError>.Failure itemFailure)
            {
                throw new InvalidOperationException(itemFailure.Error.ToString());
            }
        }
    }

    /// <summary>Seeds the persisted content type which exposes the Surreal View editor tab.</summary>
    public async Task SeedSurrealViewContentTypeAsync(string alias)
    {
        if (_app is null) throw new InvalidOperationException("The E2E application has not been initialized.");

        await using var scope = _app.Services.CreateAsyncScope();
        var contentTypes = scope.ServiceProvider.GetRequiredService<IContentTypeService>();
        var existing = await contentTypes.GetByAliasAsync(SiteId, alias);
        if (existing is Result<ContentTypeDefinition, AeroError>.Ok) return;

        var result = await contentTypes.SaveAsync(new ContentTypeDefinition
        {
            Id = 0,
            SiteId = SiteId,
            Alias = alias,
            Name = "Surreal View E2E records",
            Description = "Browser fixture for generic query-backed content.",
            Fields =
            [
                new ContentFieldDefinition { Name = "title", Label = "Title", FieldType = "text", Required = true }
            ]
        });
        if (result is Result<ContentTypeDefinition, AeroError>.Failure failure)
            throw new InvalidOperationException(failure.Error.ToString());
    }

    /// <summary>Seeds a generic localized entry and its published query-backed reference provider.</summary>
    public async Task<LocalizationContentEntrySeed> SeedLocalizedContentEntryAsync(string alias)
    {
        if (_app is null) throw new InvalidOperationException("The E2E application has not been initialized.");

        await using var scope = _app.Services.CreateAsyncScope();
        var contentTypes = scope.ServiceProvider.GetRequiredService<IContentTypeService>();
        var contentItems = scope.ServiceProvider.GetRequiredService<IContentService>();
        var commands = scope.ServiceProvider.GetRequiredService<ContentCommandService>();
        var views = scope.ServiceProvider.GetRequiredService<IContentSurrealViewStore>();
        var shape = scope.ServiceProvider.GetServices<IContentShape>()
            .Single(candidate => candidate.Definition.Alias == "e2e-record").Definition;
        var viewScope = new ContentViewScope(TenantId, SiteId);
        var viewAlias = $"{alias}-records";

        if (await contentTypes.GetByAliasAsync(SiteId, alias) is Result<ContentTypeDefinition, AeroError>.Failure)
        {
            var typeResult = await contentTypes.SaveAsync(new ContentTypeDefinition
            {
                SiteId = SiteId,
                Alias = alias,
                Name = "Localized reference records",
                Description = "Disposable browser fixture for generic content localization.",
                AllowPublicUrl = true,
                Fields =
                [
                    new ContentFieldDefinition { Name = "shared-code", Label = "Shared code", FieldType = "text", LocalizationMode = ContentFieldLocalizationMode.Shared },
                    new ContentFieldDefinition { Name = "localized-name", Label = "Localized name", FieldType = "text", LocalizationMode = ContentFieldLocalizationMode.Localized, Required = true },
                    new ContentFieldDefinition { Name = "fork-note", Label = "Fork note", FieldType = "text", LocalizationMode = ContentFieldLocalizationMode.CopyOnFork },
                    new ContentFieldDefinition
                    {
                        Name = "related-entry", Label = "Related entry", FieldType = ContentFieldTypes.Reference,
                        LocalizationMode = ContentFieldLocalizationMode.Localized,
                        Settings = new Dictionary<string, JsonElement>
                        {
                            [ReferenceContentFieldSettings.TargetKind] = JsonSerializer.SerializeToElement(ReferenceContentFieldSettings.TargetKindContentEntry),
                            [ReferenceContentFieldSettings.AllowedProviders] = JsonSerializer.SerializeToElement(new[] { $"view:{viewAlias}" }),
                            ["previewFields"] = JsonSerializer.SerializeToElement(new[] { "title", "kind" })
                        }
                    }
                ]
            });
            if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure failure)
                throw new InvalidOperationException(failure.Error.ToString());
        }

        if (await views.LoadAsync(viewScope, viewAlias, ContentViewPublicationState.Published) is null)
        {
            var draft = await views.SaveDraftAsync(new ContentSurrealViewRevision(
                0, viewScope, viewAlias, shape.Alias, shape.SchemaFingerprint,
                "SELECT id, title, kind FROM e2e_records WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20",
                "id", "title", 0, ContentViewPublicationState.Draft, DateTimeOffset.UtcNow,
                EntrySelectStatement: "SELECT id, title, kind FROM e2e_records WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1",
                SearchSelectStatement: "SELECT id, title, kind FROM e2e_records WHERE tenant_id = $tenantId AND site_id = $siteId AND title CONTAINS $search LIMIT 20"));
            _ = await views.PublishAsync(viewScope, viewAlias, draft.Version);
        }

        var slug = "rtl-localized-reference";
        var item = await contentItems.GetBySlugAndTypeAsync(SiteId, alias, "ar-SA", slug);
        long itemId;
        if (item is Result<ContentItem, AeroError>.Ok existing)
        {
            itemId = existing.Value.Id;
        }
        else
        {
            var created = new ContentItem
            {
                Id = 0, SiteId = SiteId, ContentTypeAlias = alias, Culture = "ar-SA", Slug = slug,
                Title = "سجل مرجعي", VersionNumber = 0,
                Fields = new Dictionary<string, JsonElement>
                {
                    ["shared-code"] = JsonSerializer.SerializeToElement("shared-e2e"),
                    ["localized-name"] = JsonSerializer.SerializeToElement("سجل مرجعي"),
                    ["fork-note"] = JsonSerializer.SerializeToElement("Copied only when a culture is forked."),
                    ["related-entry"] = JsonSerializer.SerializeToElement(
                        new ContentEntryKey($"view:{viewAlias}", "e2e-entry"),
                        ContentJsonContext.Default.ContentEntryKey)
                }
            };
            var saved = await commands.SaveDraftAsync(created);
            if (saved is not Result<ContentItem, AeroError>.Ok savedOk) throw new InvalidOperationException(saved.ToString());
            var published = await commands.PublishAsync(savedOk.Value);
            if (published is Result<ContentItem, AeroError>.Failure publishFailure)
                throw new InvalidOperationException(DescribeError(publishFailure.Error));
            itemId = savedOk.Value.Id;
        }

        var aiBlockedSlug = "ai-review-blocked";
        var aiBlocked = await contentItems.GetBySlugAndTypeAsync(SiteId, alias, "ar-SA", aiBlockedSlug);
        long aiBlockedItemId;
        if (aiBlocked is Result<ContentItem, AeroError>.Ok existingAiBlocked)
        {
            aiBlockedItemId = existingAiBlocked.Value.Id;
        }
        else
        {
            var draft = new ContentItem
            {
                Id = 0, SiteId = SiteId, ContentTypeAlias = alias, Culture = "ar-SA", Slug = aiBlockedSlug,
                Title = "مراجعة مطلوبة", VersionNumber = 1,
                TranslationProvenance = new ContentTranslationProvenance(
                    ContentTranslationOrigin.AiAssisted, "en-US", 1, DateTimeOffset.UtcNow),
                TranslationReview = new ContentTranslationReview(),
                Fields = new Dictionary<string, JsonElement>
                {
                    ["localized-name"] = JsonSerializer.SerializeToElement("مراجعة مطلوبة"),
                    ["related-entry"] = JsonSerializer.SerializeToElement(
                        new ContentEntryKey($"view:{viewAlias}", "e2e-entry"),
                        ContentJsonContext.Default.ContentEntryKey)
                }
            };
            var saved = await contentItems.SaveAsync(draft);
            if (saved is Result<ContentItem, AeroError>.Failure aiFailure) throw new InvalidOperationException(aiFailure.Error.ToString());
            aiBlockedItemId = draft.Id;
        }

        return new LocalizationContentEntrySeed(itemId, aiBlockedItemId, alias, slug, viewAlias);
    }

    private static string DescribeError(AeroError error) =>
        error is AeroError.Validation validation
            ? string.Join(Environment.NewLine, validation.Errors)
            : error.ToString();

    private static HtmlPageContent CreateTestPageContent(
        string title,
        string summary)
    {
        var content = new HtmlPageContent();
        var main = HtmlNode.CreateElement("main");
        var section = HtmlNode.CreateElement("section");
        var heading = HtmlNode.CreateElement("h1");
        var paragraph = HtmlNode.CreateElement("p");

        heading.Children.Add(HtmlNode.CreateText(title));
        paragraph.Children.Add(HtmlNode.CreateText(summary));
        section.Children.Add(heading);
        section.Children.Add(paragraph);
        main.Children.Add(section);
        content.Root.Children.Add(main);

        return content;
    }

    private sealed class E2ESurrealViewShape : IContentShape
    {
        public ContentShapeDefinition Definition { get; } = CreateDefinition();

        private static ContentShapeDefinition CreateDefinition()
        {
            var definition = new ContentShapeDefinition(
                "e2e-record",
                [
                    new ContentShapeField("id", ContentShapeFieldType.String, Required: true),
                    new ContentShapeField("title", ContentShapeFieldType.String, Required: true),
                    new ContentShapeField("kind", ContentShapeFieldType.String)
                ],
                string.Empty);
            return definition with { SchemaFingerprint = ContentShapeFingerprint.Create(definition) };
        }
    }

    private sealed class E2ESurrealViewSource : IContentViewSource
    {
        public ContentViewSourceDefinition Definition { get; } = new(
            "e2e_records",
            "e2e_records");
    }

    private sealed class E2EReadOnlyContentViewExecutor : IReadOnlyContentViewExecutor, IAdminReadOnlyContentViewExecutor
    {
        public bool IsReadOnlyGuaranteed => true;

        public Task<ContentViewExecutionResult> ExecuteAsync(ContentViewExecutionRequest request, CancellationToken ct = default)
        {
            var rows = new[]
            {
                (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = "e2e-entry",
                    ["title"] = "Sample entry",
                    ["kind"] = "fixture"
                }
            };

            if (request.Parameters.TryGetValue("$entryId", out var entryId)
                && !string.Equals(entryId?.ToString(), "e2e-entry", StringComparison.Ordinal))
            {
                rows = [];
            }

            return Task.FromResult(new ContentViewExecutionResult(rows, false));
        }

        Task<ContentViewExecutionResult> IAdminReadOnlyContentViewExecutor.ExecuteAsync(
            ContentSurrealViewRevision view,
            ContentViewScope scope,
            IReadOnlyDictionary<string, object?> parameters,
            ContentViewExecutionLimits limits,
            CancellationToken ct)
            => Task.FromResult(new ContentViewExecutionResult(
            [
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = "e2e-entry",
                    ["title"] = "Sample entry",
                    ["kind"] = "fixture"
                }
            ],
            false));
    }

    private sealed class E2EAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, E2EUserId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, "admin@aero.local"),
                new Claim(ClaimTypes.Email, "admin@aero.local"),
                new Claim(ClaimTypes.Role, CmsRoleNames.Admin)
            ], Scheme.Name);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private static async Task EnsureDatabaseAsync(string dbName, int port)
    {
        await using var conn = new NpgsqlConnection(
            $"Host=localhost;Port={port};Username={dbName};Database=postgres;");
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", conn);
        cmd.Parameters.AddWithValue("name", dbName);
        if (await cmd.ExecuteScalarAsync() is true) return;

        await using var createCmd = new NpgsqlCommand(
            $"CREATE DATABASE {dbName} OWNER {dbName}", conn);
        await createCmd.ExecuteNonQueryAsync();
    }

    private static string AllocateLoopbackBaseUrl()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            // Chromium rejects historical service ports such as 6667 before
            // issuing a request. Avoid the observed unsafe port without
            // assuming a particular OS ephemeral-port range.
            if (port != 6667)
                return $"http://localhost:{port}";
        }

        throw new InvalidOperationException("Unable to allocate a browser-safe loopback port for the E2E host.");
    }

    // ── Cleanup ──────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        if (Page is not null) await Page.CloseAsync();
        if (BrowserContext is not null) await BrowserContext.DisposeAsync();
        if (Browser is not null) await Browser.DisposeAsync();
        PlaywrightInstance?.Dispose();

        // Stop the background app
        if (_appCts is not null)
        {
            _appCts.Cancel();
            if (_appTask is not null)
            {
                try { await _appTask; }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Warning(ex, "App background task threw on shutdown"); }
            }
            _appCts.Dispose();
        }

        if (_app is not null) await _app.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.StopAsync();
            await _postgres.DisposeAsync();
        }
    }
}
