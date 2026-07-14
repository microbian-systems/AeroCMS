using Aero.AppServer;
using Aero.AppServer.Startup;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Contracts.Abstractions;
using Aero.Cms.Contracts.Services;
using Aero.Cms.Core;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
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
public static class AeroCmsExtensions
{
        /// <summary>
    /// AddAeroCmsAsync method.
    /// </summary>
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
        config["ConnectionStrings:aero"] = resolvedInfrastructure.DatabaseConnectionString;

        if (!string.IsNullOrWhiteSpace(resolvedInfrastructure.CacheConnectionString))
        {
            config["ConnectionStrings:cache"] = resolvedInfrastructure.CacheConnectionString;
        }

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
            });

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy("AeroAdmin", policy => policy.RequireRole("Admin"));
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
            .AddApplicationPart(typeof(BlockBase).Assembly)
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

        var (_, log) = await builder.AddAeroCmsRuntimeAsync<TProgram>(options.ModuleDescriptors);
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

        /// <summary>
    /// RunAeroCmsAsync method.
    /// </summary>
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

        try
        {
            log.Information("Starting main Aero application host...");
            await app.StartAsync();

            try
            {
                await AeroStartupPipeline.WaitForRequiredInfrastructureAsync(app, bootstrapState, log);

                log.Information("Applying runtime preparation...");
                await app.PrepareAeroAppAsync();

                if (bootstrapState.IsConfiguredMode)
                {
                    await using var runtimeBootstrapScope = app.Services.CreateAsyncScope();
                    var initializer = runtimeBootstrapScope.ServiceProvider.GetService<IRuntimeBootstrapInitializer>();
                    if (initializer is not null)
                    {
                        log.Information("Running runtime bootstrap initializer...");
                        await initializer.InitializeAsync();
                        log.Information("Runtime bootstrap initialization completed.");
                    }
                }

                log.Information("Initializing runtime services...");
                await app.InitializeAeroAppAsync();

                await app.WaitForShutdownAsync();
            }
            catch (Exception ex) when (bootstrapState.IsConfiguredMode)
            {
                log.Error(ex, "Bootstrap initialization failed: {Message}", ex.Message);
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
            log.Fatal(ex, "Error starting the main Aero CMS application");
            throw;
        }
        finally
        {
            log.Information("Main application exiting");
        }
    }

        /// <summary>
    /// ConfigureAeroCmsBootstrapLogging method.
    /// </summary>
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
