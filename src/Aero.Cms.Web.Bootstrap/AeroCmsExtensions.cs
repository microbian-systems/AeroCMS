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
/// <see cref="AddAeroCmsAsync{TProgram}(WebApplicationBuilder, string[], Action{AeroCmsOptions})"/>
/// performs the one-time setup handoff and configures the host builder.
/// <see cref="UseAeroCms(WebApplication)"/> builds the middleware pipeline and
/// <see cref="MapAeroCms{TRootComponent}(WebApplication, Action{RazorComponentsEndpointConventionBuilder})"/>
/// maps the framework endpoints. The consumer retains the standard ASP.NET Core application lifetime.
/// </remarks>
public static class AeroCmsExtensions
{
    /// <summary>
    /// Runs the one-time setup handoff when required and adds Aero CMS to a normal ASP.NET Core host.
    /// </summary>
    /// <typeparam name="TProgram">The host application's entry-point marker type.</typeparam>
    /// <param name="builder">The normal web application builder.</param>
    /// <param name="args">The application's command-line arguments.</param>
    /// <param name="configure">Configures generated catalogs and optional Aero CMS integrations.</param>
    /// <returns>The same configured builder.</returns>
    /// <remarks>
    /// The method is asynchronous because first run may serve the existing setup wizard and wait for
    /// its explicit handoff. Once configured, startup follows the ordinary ASP.NET Core builder,
    /// middleware, endpoint, and <c>RunAsync</c> lifecycle.
    /// </remarks>
    public static async Task<WebApplicationBuilder> AddAeroCmsAsync<TProgram>(
        this WebApplicationBuilder builder,
        string[] args,
        Action<AeroCmsOptions> configure)
        where TProgram : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configure);

        ConfigureAeroCmsBootstrapLogging(builder.Environment.ContentRootPath);
        await AeroStartupPipeline.EnsureRuntimeConfigurationAsync(builder, args);
        _ = await builder.AddAeroCmsAsync<TProgram>(configure);
        return builder;
    }

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
    /// runtime services; startup initialization is performed by a hosted service, while middleware and
    /// endpoints are configured by <see cref="UseAeroCms(WebApplication)"/> and
    /// <see cref="MapAeroCms{TRootComponent}(WebApplication, Action{RazorComponentsEndpointConventionBuilder})"/>.
    /// Hydro services are registered only when <see cref="AeroCmsOptions.EnableHydro"/> is enabled.
    /// <para>
    /// Authentication defaults to the <c>AeroCms.Manager</c> policy scheme for the general,
    /// authenticate, and challenge schemes, while sign-in defaults to <c>Identity.Application</c>
    /// (<see cref="IdentityConstants.ApplicationScheme"/>). The application cookie defaults to <c>.AeroCms.Auth</c>,
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
                authentication.DefaultScheme = ManagerRecoveryDefaults.ManagerScheme;
                authentication.DefaultAuthenticateScheme = ManagerRecoveryDefaults.ManagerScheme;
                authentication.DefaultChallengeScheme = ManagerRecoveryDefaults.ManagerScheme;
                authentication.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddPolicyScheme(ManagerRecoveryDefaults.ManagerScheme, null, ManagerAuthenticationSchemeRouting.Configure)
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
                var existingValidator = cookie.Events.OnValidatePrincipal;
                cookie.Events.OnValidatePrincipal = async context =>
                {
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                    if (context.Principal is null)
                        return;

                    if (existingValidator is not null)
                        await existingValidator(context);

                    if (context.Principal is null)
                        return;

                    await context.HttpContext.RequestServices
                        .GetRequiredService<ManagerFederationCookieValidator>()
                        .ValidateAsync(context);
                };
            })
            .AddCookie(ExternalMemberAuthenticationDefaults.Scheme, cookie =>
            {
                cookie.Cookie.Name = ".AeroCms.Member";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.LoginPath = "/shop/account";
                cookie.AccessDeniedPath = "/shop/account";
                cookie.SlidingExpiration = false;
                cookie.Events.OnValidatePrincipal = context =>
                    context.HttpContext.RequestServices
                        .GetRequiredService<ExternalMemberCookieValidator>()
                        .ValidateAsync(context);
            })
            .AddCookie(ManagerRecoveryDefaults.Scheme, cookie =>
            {
                cookie.Cookie.Name = ManagerRecoveryDefaults.CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.SlidingExpiration = false;
                cookie.ExpireTimeSpan = ManagerRecoveryDefaults.SessionLifetime;
                cookie.LoginPath = "/manager/recovery";
            });

        services.AddAuthorization(authorization =>
        {
            var managerPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                    ManagerRecoveryDefaults.ManagerScheme)
                .RequireAuthenticatedUser()
                .Build();
            authorization.AddPolicy(ManagerRecoveryDefaults.ManagerPolicy, managerPolicy);
            authorization.DefaultPolicy = managerPolicy;

            authorization.AddPolicy("AeroAdmin", policy =>
            {
                policy.AddAuthenticationSchemes(ManagerRecoveryDefaults.ManagerScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });
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
        RuntimeBootstrapReadinessStartupFilterOrdering.MoveReadinessFilterToStart(services);
        services.AddSingleton(options.ModuleDescriptors);
        services.AddSingleton(options);
        services.AddHostedService<AeroCmsRuntimeInitializationHostedService>();

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
    /// Adds the Aero CMS middleware pipeline to a built ASP.NET Core application.
    /// </summary>
    /// <param name="app">The application to configure.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseAeroCms(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var infrastructure = app.Services.GetRequiredService<ResolvedInfrastructureSettings>();

        app.UseExceptionHandler();
        if (string.Equals(infrastructure.DatabaseMode, "Embedded", StringComparison.OrdinalIgnoreCase))
        {
            app.UseMiddleware<RequestCancellationIsolationMiddleware>();
        }

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

        if (app.Environment.IsDevelopment())
        {
            app.Use(static async (context, next) =>
            {
                var isFrameworkAsset = context.Request.Path.StartsWithSegments(
                    "/_framework",
                    StringComparison.OrdinalIgnoreCase);
                var isManagerDocument = HttpMethods.IsGet(context.Request.Method) &&
                    context.Request.Path.StartsWithSegments(
                        "/manager",
                        StringComparison.OrdinalIgnoreCase);

                if (isFrameworkAsset)
                {
                    context.Response.OnStarting(static state =>
                    {
                        var response = (HttpResponse)state;
                        response.Headers.CacheControl = "no-store, no-cache, max-age=0";
                        response.Headers.Pragma = "no-cache";
                        response.Headers.Expires = "0";
                        return Task.CompletedTask;
                    }, context.Response);
                }
                else if (isManagerDocument)
                {
                    context.Response.OnStarting(static state =>
                    {
                        var response = (HttpResponse)state;
                        response.Headers["Clear-Site-Data"] = "\"cache\"";
                        return Task.CompletedTask;
                    }, context.Response);
                }

                await next(context);
            });
        }

        app.UseStaticFiles();

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
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseCmsSetupGate();
        app.UseAeroCmsModulePipeline();
        app.UseAntiforgery();

        return app;
    }

    /// <summary>
    /// Maps Aero CMS framework, Razor, component, identity, module, and API-reference endpoints.
    /// </summary>
    /// <typeparam name="TRootComponent">The host application's root Razor component.</typeparam>
    /// <param name="app">The application to configure.</param>
    /// <param name="configureComponents">Optionally adds host-owned component assemblies.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapAeroCms<TRootComponent>(
        this WebApplication app,
        Action<RazorComponentsEndpointConventionBuilder>? configureComponents = null)
        where TRootComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetService<AeroCmsOptions>() ?? new AeroCmsOptions();
        app.MapDefaultEndpoints();
        app.MapStaticAssets();

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
        app.MapExternalMemberLocalApi();
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

        return app;
    }

    /// <summary>
    /// Compatibility wrapper for hosts using the former all-in-one runtime method.
    /// </summary>
    [Obsolete("Use app.UseAeroCms(), app.MapAeroCms<TRootComponent>(), and await app.RunAsync().")]
    public static Task RunAeroCmsAsync<TRootComponent>(
        this WebApplication app,
        BootstrapState bootstrapState,
        Serilog.ILogger log,
        Action<RazorComponentsEndpointConventionBuilder>? configureComponents = null)
        where TRootComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(bootstrapState);
        ArgumentNullException.ThrowIfNull(log);

        app.UseAeroCms();
        app.MapAeroCms<TRootComponent>(configureComponents);
        return app.RunAsync();
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
