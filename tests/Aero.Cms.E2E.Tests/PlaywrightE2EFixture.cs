using Aero.AppServer;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Web.Bootstrap;
using Aero.Cms.Web.Core.Eextensions;
using Aero.Core;
using Aero.Core.Identity;
using Aero.Models.Entities;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using MysticMind.PostgresEmbed;
using Npgsql;
using JasperFx;
using Scalar.AspNetCore;
using Serilog;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Wolverine;
using Orleans.Runtime;
using Aero.Modular;
using Hydro.Configuration;

namespace Aero.Cms.E2E.Tests;

/// <summary>
/// E2E test fixture that starts a real Kestrel-hosted WebApplication (not Alba/TestServer).
///
/// Why not Alba? Alba uses <c>TestServer</c> internally, which handles requests in-memory
/// via a special HttpClient. Playwright (real browser) needs a real TCP listener.
///
/// Startup sequence:
///   1. Start embedded Postgres via MysticMind.PostgresEmbed
///   2. Build a WebApplication using the real <c>AddAeroCmsAsync</c> DI setup
///   3. Configure the middleware pipeline (mirrors <c>RunAeroCmsAsync</c> minus HTTPS)
///   4. Start the app via <c>app.StartAsync()</c> on a background thread
///   5. Poll for HTTP readiness
///   6. Start Playwright
///   7. On dispose: stop the app and clean up all resources
/// </summary>
public sealed class PlaywrightE2EFixture : IAsyncDisposable
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private PgServer? _postgres;
    private int _pgPort;
    private IDocumentStore? _store;
    private WebApplication? _app;
    private CancellationTokenSource? _appCts;
    private Task? _appTask;

    public string BaseUrl { get; private set; } = "http://localhost:5555";
    public IPage? Page { get; private set; }
    public IBrowser? Browser { get; private set; }
    public IBrowserContext? BrowserContext { get; private set; }
    public IPlaywright? PlaywrightInstance { get; private set; }
    public long HomePageId { get; private set; }
    public long BlockPageId { get; private set; }

    public async Task InitializeAsync()
    {
        await Lock.WaitAsync();
        try
        {
            if (_app is not null) return;

            // ── 1. Start embedded Postgres ──────────────────────────────
            const string dbName = "aero_e2e_test";
            _pgPort = 5436;
            _postgres = new PgServer("18.3.0", dbName, port: _pgPort, clearInstanceDirOnStop: true);
            await _postgres.StartAsync();
            await EnsureDatabaseAsync(dbName, _pgPort);

            var connString = $"Host=localhost;Port={_pgPort};Username={dbName};Database={dbName};";

            // ── 2. Marten store (schema creation, seed runs after app starts) ─
            _store = DocumentStore.For(options =>
            {
                options.Connection(connString);
                options.DatabaseSchemaName = "aero";
                options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
            });

            // ── 3. Build & start the real web app with Kestrel ───────────
            var webProjectPath = Aero.Cms.Modules.Setup.Configuration.AppSettingsPathResolver.GetWebProjectPath();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = webProjectPath,
                EnvironmentName = Environments.Development,
                ApplicationName = "Aero.Cms.Web"
            });

            // Override config for testing
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:aero"] = connString,
                ["AeroCms:Bootstrap:State"] = "Running",
                ["AeroCms:Bootstrap:SetupComplete"] = "true",
                ["AeroCms:Bootstrap:SeedComplete"] = "true",
                        ["AeroCms:Bootstrap:DatabaseMode"] = "Embedded",
                ["AeroCms:Bootstrap:HasBootstrapConfig"] = "true",
                ["AeroCms:Bootstrap:AuthenticationMode"] = "Local",
                ["AeroCms:Bootstrap:CacheMode"] = "Memory",
                ["AeroCms:Bootstrap:SecretProvider"] = "Local Certificate",
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
            AeroCmsExtensions.ConfigureAeroCmsBootstrapLogging(webProjectPath);

            // Load module descriptors via reflection — avoids CS0433 type collisions from
            // the source generator that emits GeneratedAeroModuleCatalog / GeneratedWolverineHandlerCatalog
            // into every project referencing Aero.Modular.
            var (descriptors, configureWolverine, configureGrains) = LoadGeneratedCatalogs();

            // Call the real startup to register all services
            await builder.AddAeroCmsAsync<Program>(options =>
            {
                options.ModuleDescriptors = descriptors;
                options.ConfigureWolverine = configureWolverine;
                options.ConfigureGrains = configureGrains;
                options.ConfigureApplicationCookie = cookie =>
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            // Remove the embedded DB background service (already have PG running externally)
            var embeddedDbDescriptor = builder.Services.FirstOrDefault(d =>
                d.ImplementationType?.Name == "AeroEmbeddedDbService");
            if (embeddedDbDescriptor is not null)
                builder.Services.Remove(embeddedDbDescriptor);

            _app = builder.Build();

            // Configure middleware pipeline (mirrors RunAeroCmsAsync without HTTPS)
            ConfigureTestMiddleware(_app);

            // Start the app on a background thread
            _appCts = new CancellationTokenSource();
            _appTask = Task.Run(async () =>
            {
                try
                {
                    await _app.StartAsync(_appCts.Token);

                    // Optional post-start initialization (migrations, modules)
                    // Non-fatal — app stays up even if these fail.
                    try { await _app.PrepareAeroAppAsync(); }
                    catch (Exception ex) { Log.Warning(ex, "PrepareAeroAppAsync failed (non-fatal)"); }

                    try { await _app.InitializeAeroAppAsync(); }
                    catch (Exception ex) { Log.Warning(ex, "InitializeAeroAppAsync failed (non-fatal)"); }

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
            Page = await BrowserContext.NewPageAsync();
        }
        finally
        {
            Lock.Release();
        }
    }

    // ── Generated catalog loader (reflection-based) ──────────────────────

    /// <summary>
    /// Loads source-generated catalogs from the web project's assembly via reflection.
    ///
    /// Avoids CS0433: the source generator emits the same type names
    /// (GeneratedAeroModuleCatalog, GeneratedWolverineHandlerCatalog) into every
    /// project that references Aero.Modular, making compile-time resolution ambiguous.
    /// Runtime reflection picks the correct copy from the <c>Aero.Cms.Web</c> assembly.
    /// </summary>
    private static (IReadOnlyList<ModuleDescriptor> Descriptors,
                    Action<WolverineOptions> ConfigureWolverine,
                    Action<ISiloBuilder> ConfigureGrains) LoadGeneratedCatalogs()
    {
        var webAssembly = typeof(Program).Assembly;

        // ModuleDescriptors from GeneratedAeroModuleCatalog.Descriptors
        IReadOnlyList<ModuleDescriptor> descriptors = [];
        var moduleCatalogType = webAssembly.GetType("Aero.Cms.Web.Generated.GeneratedAeroModuleCatalog");
        if (moduleCatalogType is not null)
        {
            var prop = moduleCatalogType.GetProperty("Descriptors", BindingFlags.Public | BindingFlags.Static);
            if (prop?.GetValue(null) is IReadOnlyList<ModuleDescriptor> list)
                descriptors = list;
        }

        // Wolverine registration callback from GeneratedWolverineHandlerCatalog.Register
        Action<WolverineOptions> configureWolverine = static _ => { };
        var wolverineCatalogType = webAssembly.GetType("Aero.Cms.Web.Generated.GeneratedWolverineHandlerCatalog");
        if (wolverineCatalogType is not null)
        {
            var method = wolverineCatalogType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            if (method is not null)
                configureWolverine = method.CreateDelegate<Action<WolverineOptions>>();
        }

        // Grain registration callback from GeneratedAeroGrainCatalog.Register
        Action<ISiloBuilder> configureGrains = static _ => { };
        var grainCatalogType = webAssembly.GetType("Aero.Cms.Web.Generated.GeneratedAeroGrainCatalog");
        if (grainCatalogType is not null)
        {
            var method = grainCatalogType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            if (method is not null)
                configureGrains = method.CreateDelegate<Action<ISiloBuilder>>();
        }

        return (descriptors, configureWolverine, configureGrains);
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
        var env = app.Environment;
        var options = app.Services.GetRequiredService<AeroCmsOptions>();

        app.UseExceptionHandler();

        if (env.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            // HSTS skipped for testing
        }

        // HTTPS redirection skipped — Playwright tests use HTTP.

        app.UseAeroApplicationServer();
        app.UseStatusCodePagesWithReExecute("/oops", "?status={0}");
        app.Use(static async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                var statusCodePagesFeature = context.Features.Get<IStatusCodePagesFeature>();
                if (statusCodePagesFeature is not null)
                {
                    statusCodePagesFeature.Enabled = false;
                }
            }

            await next(context);
        });

        app.UseStaticFiles();
        app.MapStaticAssets();

        app.UseRouting();
        app.UseRequestLocalization(localization =>
        {
            var supportedCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures | CultureTypes.NeutralCultures)
                .Where(culture => !string.IsNullOrWhiteSpace(culture.Name))
                .ToArray();

            localization.DefaultRequestCulture = new RequestCulture("en-US");
            localization.SupportedCultures = supportedCultures;
            localization.SupportedUICultures = supportedCultures;
            localization.ApplyCurrentCultureToResponseHeaders = true;

            localization.RequestCultureProviders.Clear();
            localization.RequestCultureProviders.Add(new Aero.Cms.Web.Bootstrap.Localization.AeroRequestCultureProvider());
            localization.RequestCultureProviders.Add(new CookieRequestCultureProvider());
            localization.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
            localization.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCmsSetupGate();
        app.UseAeroCmsModulePipeline();
        app.UseAntiforgery();

        // Culture cookie setter
        app.MapGet("/culture/set", (string culture, string returnUrl, HttpContext context) =>
        {
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });

            return Results.Redirect(returnUrl);
        });

        app.MapRazorPages();

        var componentBuilder = app.MapRazorComponents<Aero.Cms.Web.Components.App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(Aero.Cms.Shared._Imports).Assembly,
                typeof(Aero.Cms.Web.Client._Imports).Assembly,
                typeof(SetupModule).Assembly);

        Aero.Cms.Modules.Identity.IdentityApi.MapIdentityApi(app);
        app.MapAeroCmsEndpoints();

        if (options.EnableOpenApi)
        {
            app.MapOpenApi();
        }

        if (options.EnableScalarApiReference)
        {
            app.MapScalarApiReference(scalar =>
            {
                scalar.WithTitle("Aero CMS")
                    .ForceDarkMode()
                    .HideSearch()
                    .ShowOperationId()
                    .ExpandAllTags()
                    .SortTagsAlphabetically()
                    .SortOperationsByMethod()
                    .PreserveSchemaPropertyOrder();
            });
        }

        if (options.EnableHydro)
        {
            // Hydro MUST be last — it internally calls UseEndpoints().
            app.UseHydro(app.Environment);
        }
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
            try
            {
                var response = await client.GetAsync($"{BaseUrl}/manager/login");
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect)
                    return;
            }
            catch
            {
                // Server not ready yet
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"App did not start within {maxRetries} seconds at {BaseUrl}");
    }

    // ── Login helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Logs in via the browser login page using real form-fill interaction.
    /// If already authenticated (redirected away from /manager/login), returns immediately.
    /// </summary>
    public async Task LoginAsync()
    {
        if (Page is null) throw new InvalidOperationException("Page not initialized");

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

        var loginInputs = loginForm.Locator("input");
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
    /// and the app-configured <c>IDocumentStore</c>) is available.
    ///
    /// The configuration keys <c>SetupComplete</c> / <c>SeedComplete</c> are set
    /// to <c>true</c> so the app does <b>not</b> redirect to the setup wizard;
    /// this method performs the equivalent work programmatically.
    /// </summary>
    private async Task SeedDatabaseAfterStartAsync()
    {
        using var scope = _app!.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var roleManager   = sp.GetRequiredService<RoleManager<AeroRole>>();
        var userManager   = sp.GetRequiredService<UserManager<AeroUser>>();
        var documentStore = sp.GetRequiredService<IDocumentStore>();

        // ── Roles ───────────────────────────────────────────────────────
        foreach (var roleName in AeroCmsRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new AeroRole(roleName);
                var result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to create role '{roleName}': " +
                        string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        // ── Admin user ──────────────────────────────────────────────────
        var adminUser = await userManager.FindByEmailAsync("admin@aero.local");
        if (adminUser is null)
        {
            adminUser = new AeroUser
            {
                Id             = Snowflake.NewId(),
                UserName       = "admin@aero.local",
                Email          = "admin@aero.local",
                EmailConfirmed = true,
                FirstName      = "Admin",
                LastName       = "User",
                IsActive       = true
            };

            var createResult = await userManager.CreateAsync(adminUser, "Admin123!");
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    "Failed to create admin user: " +
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));

            var roleResult = await userManager.AddToRoleAsync(adminUser, AeroCmsRoles.Admin);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(
                    "Failed to assign Admin role: " +
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        // ── Site (tenant) ───────────────────────────────────────────────
        await using var session = documentStore.LightweightSession();

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
            .FirstOrDefaultAsync(a => a.UserId == adminUser.Id && a.SiteId == site.Id);
        if (assignment is null)
        {
            session.Store(new UserSiteAssignment
            {
                Id          = Snowflake.NewId(),
                UserId      = adminUser.Id,
                SiteId      = site.Id,
                Permissions = ["create", "read", "update", "delete"]
            });
        }

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
                ShowHeaderNavigation = false,
                RootNodes =
                [
                    CreateBoringHeroNode(
                        "Seeded Hero Block",
                        "This hero was seeded by the E2E test fixture")
                ]
            };
            session.Store(blockPage);
        }
        BlockPageId = blockPage.Id;

        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Resets the seeded block page back to its original single "Seeded Hero Block".
    /// Call before any mutating test (duplicate, delete, etc.) to ensure a known state.
    /// </summary>
    public async Task ResetBlockPageAsync()
    {
        await using var session = _store!.LightweightSession();
        var page = await session.LoadAsync<PageDocument>(BlockPageId);
        if (page is null) return;

        page.RootNodes =
        [
            CreateBoringHeroNode(
                "Seeded Hero Block",
                "This hero was seeded by the E2E test fixture")
        ];

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
        await using var session = _store!.LightweightSession();
        var page = await session.LoadAsync<PageDocument>(HomePageId);
        if (page is null) return;
        page.RootNodes = [];
        session.Store(page);
        await session.SaveChangesAsync();
        Console.WriteLine("[Fixture] Home page reset to empty state");
    }

    private static Aero.Cms.Abstractions.Blocks.Neo.NeoPageNode CreateBoringHeroNode(
        string title,
        string summary) =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = "boring_hero",
            Kind = Aero.Cms.Abstractions.Blocks.Neo.NeoPageNodeKind.Primitive,
            Properties = new Dictionary<string, JsonElement>
            {
                ["title"] = JsonSerializer.SerializeToElement(title),
                ["summary"] = JsonSerializer.SerializeToElement(summary),
                ["fullWidth"] = JsonSerializer.SerializeToElement(true)
            },
            Children = []
        };

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

    // ── Cleanup ──────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
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
        _store?.Dispose();

        if (_postgres is not null)
        {
            await _postgres.StopAsync();
            await _postgres.DisposeAsync();
        }
    }
}
