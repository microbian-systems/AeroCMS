using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Abstractions.Http;

public static class AeroHttpClientExtensions
{
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
                .AddStandardResilienceHandler();
        });

        services.AddHttpClient<IBlogHttpClient, BlogHttpClient>();
        services.AddHttpClient<ICategoriesHttpClient, CategoriesHttpClient>();
        services.AddHttpClient<IDashboardHttpClient, DashboardHttpClient>();
        services.AddHttpClient<IFilesHttpClient, FilesHttpClient>();
        services.AddHttpClient<IMediaHttpClient, MediaHttpClient>();
        services.AddHttpClient<IModulesHttpClient, ModulesHttpClient>();
        services.AddHttpClient<INavigationsHttpClient, NavigationsHttpClient>();
        services.AddHttpClient<IPagesHttpClient, PagesHttpClient>();
        services.AddHttpClient<IProfileHttpClient, ProfileHttpClient>();
        services.AddHttpClient<ISettingsHttpClient, SettingsHttpClient>();
        services.AddHttpClient<ITagsHttpClient, TagsHttpClient>();
        services.AddHttpClient<IThemesHttpClient, ThemesHttpClient>();
        services.AddHttpClient<IUsersHttpClient, UsersHttpClient>();
        services.AddHttpClient<IBlocksHttpClient, BlocksHttpClient>();
        services.AddHttpClient<IPublishHttpClient, PublishHttpClient>();
        services.AddHttpClient<IPreviewHttpClient, PreviewHttpClient>();
        services.AddHttpClient<IDocsHttpClient, DocsHttpClient>();
        services.AddHttpClient<IAuthClient, AuthClient>();

        return services;
    }
}
