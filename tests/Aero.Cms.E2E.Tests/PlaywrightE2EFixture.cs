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
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
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
            });

            // Remove the embedded DB background service (already have PG running externally)
            var embeddedDbDescriptor = builder.Services.FirstOrDefault(d =>
                d.ImplementationType?.Name == "AeroEmbeddedDbService");
            if (embeddedDbDescriptor is not null)
                builder.Services.Remove(embeddedDbDescriptor);

            // Override auth cookie secure policy for HTTP testing
            builder.Services.PostConfigureAll<CookieAuthenticationOptions>(o =>
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);

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

    private bool _isLoggedIn;

    /// <summary>
    /// Logs in via the browser login page. Uses an isLoggedIn flag so
    /// subsequent calls on the same Page skip the login step.
    /// </summary>
    public async Task LoginAsync()
    {
        if (_isLoggedIn || Page is null) return;

        await Page.GotoAsync($"{BaseUrl}/manager/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 120000 });

        await Page.WaitForSelectorAsync("form input",
            new() { State = WaitForSelectorState.Visible, Timeout = 30000 });

        await Page.FillAsync("form input", "admin@aero.local");
        await Page.FillAsync("input[type='password']", "Admin123!");
        await Page.ClickAsync("button[type='submit']");

        await Page.WaitForURLAsync("**/manager/**", new() { Timeout = 30000 });
        _isLoggedIn = true;
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
                SupportedCultures  = ["en-US"]
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
            session.Store(new PageDocument
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
            });
        }

        await session.SaveChangesAsync();
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
