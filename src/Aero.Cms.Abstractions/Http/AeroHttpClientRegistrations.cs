using Aero.Cms.Abstractions.Http;
using Aero.Cms.Core.Http.Clients;
using Aero.Core.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using ThrowGuard;

namespace Aero.Cms.Core.Extensions;

public static class AeroHttpClientExtensions
{
    public static IServiceCollection AddAeroHttpClients(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.TryAddSingleton<ISiteContext, NoopSiteContext>();
        services.TryAddSingleton<ICorrelationIdAccessor, NoopCorrelationIdAccessor>();

        services.Configure<AeroHttpClientOptions>(
            config.GetSection("Aero:HttpClient"));

        services.AddTransient<TenantIdHandler>();
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient<JwtTokenHandler>();
        services.AddTransient<AeroHttpLoggingHandler>();
        services.AddTransient<ClientRateLimitHandler>();

        services.AddSingleton<InMemoryTokenProvider>();
        services.AddSingleton<ITokenProvider>(sp => sp.GetRequiredService<InMemoryTokenProvider>());

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

            // Backward compatibility / Fallback
            if (string.IsNullOrEmpty(url))
            {
                var config = sp.GetRequiredService<IConfiguration>();
                url = config["ApiSettings:BaseUrl"] ?? config["AeroHttpClientBaseAddress"];
            }

            ThrowGuard.Throw.IfNullOrEmpty(
                url,
                msg: "httpclient url must be valid",
                argName: nameof(options.BaseUrl));

            client.BaseAddress = new Uri(url);
        });
    }
}
