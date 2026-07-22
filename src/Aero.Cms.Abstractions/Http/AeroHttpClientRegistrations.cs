using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;

namespace Aero.Cms.Abstractions.Http;

/// <summary>
/// Represents a class for AeroHttpClientExtensions.
/// </summary>
public static class AeroHttpClientExtensions
{
        /// <summary>
    /// AddAeroHttpClients method.
    /// </summary>
public static IServiceCollection AddAeroHttpClients(
        this IServiceCollection services, Uri? baseAddress = null)
    {
        services.TryAddSingleton<ISiteContext, NoopSiteContext>();
        services.TryAddSingleton<ICorrelationIdAccessor, NoopCorrelationIdAccessor>();

        services.AddTransient<TenantIdHandler>();
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient<JwtTokenHandler>();
        services.AddTransient<AeroHttpLoggingHandler>();
        services.AddTransient<ClientRateLimitHandler>();

        services.AddSingleton<InMemoryTokenProvider>();
        services.AddSingleton<ITokenProvider>(sp => sp.GetRequiredService<InMemoryTokenProvider>());

        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.AddDefaultLogger(); ;
            builder.Services.AddRedaction();
            builder.AddExtendedHttpClientLogging();

            // TODO - verify base address is configured in program.cs or wherver the caller sets it or defaults to <base href /> for wasm clients
            // we don't configure base address here as each platform has a diff mechanism for obtaining the base address and configuring httpclient
            if(baseAddress is not null)
            {
                builder.ConfigureHttpClient(client =>
                {
                    client.BaseAddress = baseAddress;
                });
            }
            builder
                .AddHttpMessageHandler<CorrelationIdHandler>()
                .AddHttpMessageHandler<TenantIdHandler>()
                .AddHttpMessageHandler<JwtTokenHandler>()
                .AddHttpMessageHandler<AeroHttpLoggingHandler>()
                .AddHttpMessageHandler<ClientRateLimitHandler>()
                .AddStandardResilienceHandler(options =>
                {
                    // Increase timeouts for long-running operations (blog import, etc.).
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                    // Circuit breaker sampling must be ≥ 2× attempt timeout.
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                    options.Retry.DisableForUnsafeHttpMethods();
                });
        });

        services.AddHttpClient<IBlogHttpClient, BlogHttpClient>();
        services.AddHttpClient<ICategoriesHttpClient, CategoriesHttpClient>();
        services.AddHttpClient<IDashboardHttpClient, DashboardHttpClient>();
        services.AddHttpClient<IFilesHttpClient, FilesHttpClient>();
        services.AddHttpClient<IMediaHttpClient, MediaHttpClient>();
        services.AddHttpClient<IModulesHttpClient, ModulesHttpClient>();
        services.AddHttpClient<INavigationsHttpClient, NavigationsHttpClient>();
        services.AddHttpClient<IFootersHttpClient, FootersHttpClient>();
        services.AddHttpClient<IPagesHttpClient, PagesHttpClient>();
        services.AddHttpClient<IProfileHttpClient, ProfileHttpClient>();
        services.AddHttpClient<ISettingsHttpClient, SettingsHttpClient>();
        services.AddHttpClient<ISeriesHttpClient, SeriesHttpClient>();
        services.AddHttpClient<ITagsHttpClient, TagsHttpClient>();
        services.AddHttpClient<IThemesHttpClient, ThemesHttpClient>();
        services.AddHttpClient<IUsersHttpClient, UsersHttpClient>();
        services.AddHttpClient<IPublishHttpClient, PublishHttpClient>();
        services.AddHttpClient<IPreviewHttpClient, PreviewHttpClient>();
        services.AddHttpClient<IDocsHttpClient, DocsHttpClient>();
        services.AddHttpClient<IAuthClient, AuthClient>();
        services.AddHttpClient<IContentTypesHttpClient, ContentTypesHttpClient>();
        services.AddHttpClient<IContentItemsHttpClient, ContentItemsHttpClient>();
        services.AddHttpClient<ISitesHttpClient, SitesHttpClient>();
        services.AddHttpClient<IAliasHttpClient, AliasesHttpClient>();
        services.AddHttpClient<IAiHttpClient, AiHttpClient>();
        services.AddHttpClient<IExternalIdentityAdminClient, ExternalIdentityAdminClient>();

        // Register WASM-safe Contracts interface — resolves via cast to shared implementation
        services.AddScoped<Aero.Cms.Contracts.Abstractions.ISitesHttpClient>(sp =>
            (Aero.Cms.Contracts.Abstractions.ISitesHttpClient)sp.GetRequiredService<ISitesHttpClient>());

        return services;
    }
}
