using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Contracts.Abstractions;
using Aero.Cms.Contracts.Services;
using Aero.Cms.Core;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Web.Core.Diagnostics;
using Aero.Cms.Web.Core.Middleware;
using Aero.Cms.ServiceDefaults;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Shared.Services;
using Aero.Cms.Web.Bootstrap.Infrastructure;
using Aero.Cms.Web.Bootstrap.Localization;
using Aero.Cms.Web.Bootstrap.Services;
using Aero.Cms.Web.Core.Eextensions;
using Aero.Core.Http;
using Aero.Web.Exceptions;
using Hydro.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NeoUI.Blazor;
using NeoUI.Blazor.Extensions;
using NeoUI.Blazor.Primitives.Extensions;
using Radzen;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Globalization;

namespace Aero.Cms.Web.Bootstrap;

/// <summary>
/// Package-first integration entry points for hosting Aero CMS in ASP.NET Core.
/// </summary>
/// <remarks>
/// <see cref="AddAeroCmsAsync{TProgram}(WebApplicationBuilder, Action{AeroCmsOptions})"/>
/// configures the host builder. <see cref="RunAeroCmsAsync{TRootComponent}(WebApplication, BootstrapState, Serilog.ILogger, Action{RazorComponentsEndpointConventionBuilder})"/>
/// builds the request pipeline, maps endpoints, and owns the application start, initialization,
/// lifetime wait, and stop sequence.
/// </remarks>
public static class AeroCmsExtensions
{
    /// <summary>
    /// Adds Aero CMS services and host integrations to an ASP.NET Core application builder.
    /// </summary>
    /// <typeparam name="TProgram">The host application's entry-point type used to identify the runtime application.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">
    /// A callback that supplies the generated module catalog and optionally customizes host integrations.
    /// </param>
    /// <returns>
    /// A task whose result contains the same configured <paramref name="builder"/> and the logger created
    /// by runtime registration.
    /// </returns>
    /// <remarks>
    /// This method registers services, resolves and publishes the database and cache connection settings,
    /// and invokes the supplied Wolverine, Orleans, cookie, and authorization configuration callbacks.
    /// It does not build or start the application, map endpoints, wait for infrastructure, or initialize
    /// runtime services; those operations are performed by <see cref="RunAeroCmsAsync{TRootComponent}"/>.
    /// Hydro services are registered only when <see cref="AeroCmsOptions.EnableHydro"/> is enabled.
    /// <para>
    /// Authentication defaults to <c>Identity.Application</c>
    /// (<see cref="IdentityConstants.ApplicationScheme"/>) for the general, authenticate, challenge,
    /// and sign-in schemes. Its cookie defaults to <c>.AeroCms.Auth</c>,
    /// <see cref="CookieOptions.HttpOnly"/> enabled, <see cref="CookieSecurePolicy.Always"/>,
    /// <see cref="SameSiteMode.Lax"/>, seven-day sliding expiration, and <c>/manager/login</c> for
    /// both login and access-denied redirects. <see cref="AeroCmsOptions.ConfigureApplicationCookie"/>
    /// runs after these defaults and can replace them.
    /// </para>
    /// <para>
    /// No policy scheme or forwarding rule automatically selects a bearer scheme; endpoints that require
    /// bearer authentication must select it explicitly through their authentication or authorization
    /// configuration.
    /// </para>
    /// <para>
    /// This integration does not configure a Data Protection application name, key storage, or key-ring
    /// sharing. Those remain owned by the host and framework defaults. A host that requires authentication
    /// cookies to be decrypted across processes or instances must provide compatible Data Protection
    /// configuration separately.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The configuration callback does not provide <see cref="AeroCmsOptions.ModuleDescriptors"/>.
    /// </exception>
public static async Task<(WebApplicationBuilder Builder, Serilog.ILogger Log)> AddAeroCmsAsync<TProgram>(
        this WebApplicationBuilder builder,
        Action<AeroCmsOptions> configure)
        where TProgram : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AeroCmsOptions();
        configure(options);

        if (options.ModuleDescriptors is null)
        {
            throw new InvalidOperationException(
                "Aero CMS requires source-generated module descriptors. Set AeroCmsOptions.ModuleDescriptors from the host-generated catalog.");
        }

        var services = builder.Services;
        var config = builder.Configuration;
        var env = builder.Environment;

        builder.AddServiceDefaults();

        _ = await builder.AddAeroApplicationServer(
            configureWolverine: options.ConfigureWolverine,
            configureGrains: options.ConfigureGrains);

        var resolvedInfrastructure = new InfrastructureConnectionStringResolver(config).Resolve();
        PublishResolvedInfrastructure(config, resolvedInfrastructure);

        services.AddControllersWithViews();
        services.AddAuthentication(authentication =>
            {
                authentication.DefaultScheme = IdentityConstants.ApplicationScheme;
                authentication.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                authentication.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                authentication.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, cookie =>
            {
                cookie.Cookie.Name = ".AeroCms.Auth";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.LoginPath = "/manager/login";
                cookie.AccessDeniedPath = "/manager/login";
                cookie.SlidingExpiration = true;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.ConfigureApplicationCookie?.Invoke(cookie);
            })
            .AddCookie(ExternalMemberAuthenticationDefaults.Scheme, cookie =>
            {
                cookie.Cookie.Name = ".AeroCms.Member";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.SlidingExpiration = false;
                cookie.Events.OnValidatePrincipal = context =>
                    context.HttpContext.RequestServices
                        .GetRequiredService<ExternalMemberCookieValidator>()
                        .ValidateAsync(context);
            });

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy("AeroAdmin", policy => policy.RequireRole("Admin"));
            authorization.AddPolicy(ExternalMemberAuthenticationDefaults.Policy, policy =>
            {
                policy.AddAuthenticationSchemes(ExternalMemberAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context => ExternalMemberPrincipal.TryRead(context.User, out _));
            });
            authorization.AddPolicy(ExternalMemberAuthenticationDefaults.SitePolicy, policy =>
            {
                policy.AddAuthenticationSchemes(ExternalMemberAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ExternalMemberSiteRequirement());
            });
            options.ConfigureAuthorization?.Invoke(authorization);
        });

        services.AddHttpContextAccessor();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddRadzenComponents();
        services.AddNeoUIPrimitives();
        services.AddNeoUIComponents();
        // Replace DefaultLocalizer with ASP.NET Core IStringLocalizer-backed bridge
        services.Replace(ServiceDescriptor.Scoped<NeoUI.Blazor.ILocalizer, NeoUiBridgeLocalizer>());

        services.AddRazorPages()
            .AddApplicationPart(typeof(SetupModule).Assembly)
            .AddApplicationPart(typeof(Aero.Cms.Modules.Docs.DocsModule).Assembly)
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization();

        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();

        services.AddCascadingAuthenticationState();
        services.AddSingleton<IFormFactor, ServerFormFactor>();

        services.AddOpenApi();

        var baseUri = options.ApiBaseUri;
        if (baseUri is null && Uri.TryCreate(config["ApiSettings:BaseUrl"], UriKind.Absolute, out var configuredBaseUri))
        {
            baseUri = configuredBaseUri;
        }

        services.AddAeroHttpClients(baseUri);
        services.AddScoped<ManagerThemeService>();
        services.AddScoped<ManagerAssistantState>();
        services.AddScoped<Aero.Cms.Abstractions.Interfaces.ICurrentSiteAccessor, CurrentSiteAccessor>();
        services.AddScoped<Aero.Cms.Contracts.Abstractions.ICurrentSiteAccessor, CurrentSiteAccessor>();
        services.AddScoped<AppState>();
        services.AddScoped<IAdminStorage, NoopAdminStorage>();
        services.AddScoped<AdminStateContainer>();
        services.Replace(ServiceDescriptor.Scoped<ISiteContext, DefaultSiteContext>());

        services.AddProblemDetails(problemDetails =>
        {
            problemDetails.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });
        services.AddExceptionHandler<AeroGlobalExceptionHandler>();

        var (_, log) = await builder.AddAeroCmsRuntimeAsync<TProgram>(
            options.ModuleDescriptors,
            configureResolvedInfrastructure: runtimeConfig =>
                PublishResolvedInfrastructure(runtimeConfig, resolvedInfrastructure));
        services.AddSingleton(options.ModuleDescriptors);
        services.AddSingleton(options);

        if (options.EnableHydro)
        {
            // Hydro's tag helpers read HydroOptions during rendering, so its
            // services must be registered before the app is built.
            services.AddHydro();
        }

        return (builder, log);
    }

    private static void PublishResolvedInfrastructure(
        ConfigurationManager configuration,
        ResolvedInfrastructureSettings resolvedInfrastructure)
    {
        configuration["ConnectionStrings:aero"] = resolvedInfrastructure.DatabaseConnectionString;
        configuration["ConnectionStrings:cache"] = resolvedInfrastructure.CacheConnectionString;
    }

    /// <summary>
    /// Configures and runs the Aero CMS request pipeline for an already built application.
    /// </summary>
    /// <typeparam name="TRootComponent">The root Razor component mapped by the application.</typeparam>
    /// <param name="app">The application whose middleware, endpoints, and lifetime are managed.</param>
    /// <param name="bootstrapState">
    /// The early bootstrap state that controls infrastructure waiting and configured-mode initialization.
    /// </param>
    /// <param name="log">The logger used for startup, failure, and shutdown diagnostics.</param>
    /// <param name="configureComponents">
    /// An optional callback invoked after the root component endpoint is mapped and its default render modes
    /// and additional assemblies have been added.
    /// </param>
    /// <returns>A task that completes after the application receives a shutdown signal and is stopped.</returns>
    /// <remarks>
    /// Middleware and endpoints are configured before the host is started. After start, the method waits
    /// for required infrastructure, prepares the runtime, optionally invokes
    /// <see cref="IRuntimeBootstrapInitializer"/> in configured mode, initializes runtime services, and
    /// then waits for shutdown. Hydro, when enabled, is appended after all endpoint mappings.
    /// Configured-mode failures during readiness, preparation, initialization, or lifetime waiting trigger
    /// a best-effort transition to the failed bootstrap state. Startup and runtime failures are logged and
    /// rethrown; stop failures are logged as non-fatal.
    /// <para>
    /// The method maps <c>GET /culture/set</c>. That endpoint writes the framework request-culture cookie
    /// with a one-year expiration, <see cref="CookieOptions.HttpOnly"/> and
    /// <see cref="CookieOptions.Secure"/> enabled, and <see cref="SameSiteMode.Lax"/>, then redirects
    /// directly to the supplied <c>returnUrl</c>. The mapping adds no local authentication,
    /// authorization, rate-limiting, or antiforgery metadata and does not validate that
    /// <c>returnUrl</c> is local. As implemented, its state-changing GET and unrestricted redirect are
    /// cross-site request and open-redirect concerns that the host must account for if the endpoint is exposed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="app"/> or <paramref name="log"/> is <see langword="null"/>.
    /// </exception>
public static async Task RunAeroCmsAsync<TRootComponent>(
        this WebApplication app,
        BootstrapState bootstrapState,
        Serilog.ILogger log,
        Action<RazorComponentsEndpointConventionBuilder>? configureComponents = null)
        where TRootComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(log);

        var options = app.Services.GetService<AeroCmsOptions>() ?? new AeroCmsOptions();

        app.UseExceptionHandler();
        if (string.Equals(bootstrapState.DatabaseMode, "Embedded", StringComparison.OrdinalIgnoreCase))
        {
            app.UseMiddleware<RequestCancellationIsolationMiddleware>();
        }
        app.MapDefaultEndpoints();

        if (app.Environment.IsDevelopment() &&
            bool.TryParse(app.Configuration["AeroCms:EnableWebAssemblyDebugging"], out var enableWasmDebugging) &&
            enableWasmDebugging)
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
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
        app.UseRequestLocalization(options =>
        {
            // Keep middleware culture support broad until active site cultures can be aggregated at startup.
            // AeroRequestCultureProvider still restricts public content requests to the resolved site's cultures.
            var supportedCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures | CultureTypes.NeutralCultures)
                .Where(culture => !string.IsNullOrWhiteSpace(culture.Name))
                .ToArray();

            options.DefaultRequestCulture = new RequestCulture("en-US");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.ApplyCurrentCultureToResponseHeaders = true;

            // Provider chain (highest to lowest priority):
            // 1. URL prefix (custom, site-aware)
            // 2. Cookie (user persistence)
            // 3. Query string (debug/testing override)
            // 4. Accept-Language header (browser preference)
            options.RequestCultureProviders.Clear();
            options.RequestCultureProviders.Add(new AeroRequestCultureProvider());
            options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
            options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
            options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCmsSetupGate();
        app.UseAeroCmsModulePipeline();
        app.UseAntiforgery();

        // Culture cookie setter — allows the Manager UI to persist user language preference.
        app.MapGet("/culture/set", (string culture, string returnUrl, HttpContext context) =>
        {
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax });

            return Results.Redirect(returnUrl);
        });

        app.MapRazorPages();

        var componentBuilder = app.MapRazorComponents<TRootComponent>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(Aero.Cms.Shared._Imports).Assembly,
                typeof(SetupModule).Assembly);

        configureComponents?.Invoke(componentBuilder);

        app.MapIdentityApi();
        app.MapExternalMemberApi();
        app.MapAeroCmsEndpoints();

        if (options.EnableOpenApi)
        {
            app.MapOpenApi();
        }

        if (options.EnableScalarApiReference)
        {
            app.MapScalarApiReference(scalar =>
            {
                scalar.WithTitle(AeroConstants.AppName)
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
            // Hydro MUST be the LAST middleware because it internally calls
            // UseEndpoints(), which requires all endpoint mappings
            // (Razor Pages, Razor Components, Scalar) to already be registered.
            app.UseHydro(app.Environment);
        }

        var startupStage = "host startup";

        try
        {
            startupStage = "host start";
            log.Information("Starting main Aero application host...");
            await app.StartAsync();

            try
            {
                startupStage = "infrastructure readiness";
                await AeroStartupPipeline.WaitForRequiredInfrastructureAsync(app, bootstrapState, log);

                startupStage = "runtime preparation";
                log.Information("Applying runtime preparation...");
                await app.PrepareAeroAppAsync();

                if (bootstrapState.IsConfiguredMode)
                {
                    startupStage = "runtime bootstrap initialization";
                    await using var runtimeBootstrapScope = app.Services.CreateAsyncScope();
                    var initializer = runtimeBootstrapScope.ServiceProvider.GetService<IRuntimeBootstrapInitializer>();
                    if (initializer is not null)
                    {
                        log.Information("Running runtime bootstrap initializer...");
                        await initializer.InitializeAsync();
                        log.Information("Runtime bootstrap initialization completed.");
                    }
                }

                startupStage = "runtime service initialization";
                log.Information("Initializing runtime services...");
                await app.InitializeAeroAppAsync();

                startupStage = "application lifetime";
                await app.WaitForShutdownAsync();
            }
            catch (Exception ex) when (bootstrapState.IsConfiguredMode)
            {
                var rootCauses = ExceptionDiagnostics.GetRootCauses(ex);
                log.Error(
                    ex,
                    "Bootstrap initialization failed during {StartupStage}. RootCauseCount={RootCauseCount}",
                    startupStage,
                    rootCauses.Count);

                for (var index = 0; index < rootCauses.Count; index++)
                {
                    var rootCause = rootCauses[index];
                    log.Error(
                        rootCause,
                        "Bootstrap root cause {RootCauseIndex}/{RootCauseCount} during {StartupStage}: {ExceptionType}: {ExceptionMessage}",
                        index + 1,
                        rootCauses.Count,
                        startupStage,
                        rootCause.GetType().FullName,
                        rootCause.Message);
                }

                await AeroStartupPipeline.TryMarkBootstrapFailedAsync(app, log);
                throw;
            }
            finally
            {
                try
                {
                    log.Information("Stopping main Aero application host...");
                    await app.StopAsync();
                }
                catch (Exception stopEx)
                {
                    log.Warning(stopEx, "Error shutting down host (non-fatal)");
                }
            }
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "Error starting the main Aero CMS application during {StartupStage}", startupStage);
            throw;
        }
        finally
        {
            log.Information("Main application exiting");
        }
    }

    /// <summary>
    /// Replaces the global Serilog logger with the file-and-console logger used during bootstrap.
    /// </summary>
    /// <param name="webProjectPath">
    /// The web project directory beneath which daily rolling log files are written to the
    /// <c>logs</c> directory.
    /// </param>
    /// <remarks>
    /// This method changes the process-wide <see cref="Log.Logger"/> value. The file sink is shared,
    /// writes synchronously, and flushes to disk at a 15-second interval.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="webProjectPath"/> is <see langword="null"/>.
    /// </exception>
public static void ConfigureAeroCmsBootstrapLogging(string webProjectPath)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(webProjectPath, "logs", "aero-.log"),
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                buffered: false,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(15))
            .WriteTo.Console()
            .CreateLogger();
    }
}
