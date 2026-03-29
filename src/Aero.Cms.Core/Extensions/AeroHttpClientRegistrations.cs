using Aero.Cms.Core.Http.Clients;
using Microsoft.Extensions.Configuration;


namespace Aero.Cms.Core.Extensions;

public static class AeroHttpClientExtensions
{
    public static IServiceCollection AddAeroHttpClients(this IServiceCollection services, IConfiguration config)
    {
        // todo - implement this in the settings class in db
        var url = config["AeroHttpClientBaseAddress"] ?? "https://localhost:5555/api/v1";
        var uri = new Uri(url);

        services.AddHttpClient<DocsClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<BlogHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<CategoriesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<DashboardHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<FilesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<MediaHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ModulesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<NavigationsHttpClient>(c => c.BaseAddress = uri); 
        services.AddHttpClient<PagesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ProfileHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<SettingsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<TagsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ThemesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<UsersHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<BlocksHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<PublishHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<PreviewHttpClient>(c => c.BaseAddress = uri);

        services.AddTransient<IBlogHttpClient, BlogHttpClient>();
        services.AddTransient<ICategoriesHttpClient, CategoriesHttpClient>();
        services.AddTransient<IDashboardHttpClient, DashboardHttpClient>();
        services.AddTransient<IFilesHttpClient, FilesHttpClient>(); 
        services.AddTransient<IMediaHttpClient, MediaHttpClient>();
        services.AddTransient<IModulesHttpClient, ModulesHttpClient>();
        services.AddTransient<INavigationsHttpClient, NavigationsHttpClient>();
        services.AddTransient<IPagesHttpClient, PagesHttpClient>();
        services.AddTransient<IProfileHttpClient, ProfileHttpClient>();
        services.AddTransient<ISettingsHttpClient, SettingsHttpClient>();
        services.AddTransient<ITagsHttpClient, TagsHttpClient>();
        services.AddTransient<IThemesHttpClient, ThemesHttpClient>();
        services.AddTransient<IUsersHttpClient, UsersHttpClient>();
        services.AddTransient<IBlocksHttpClient, BlocksHttpClient>();
        services.AddTransient<IPublishHttpClient, PublishHttpClient>();
        services.AddTransient<IPreviewHttpClient, PreviewHttpClient>();

        return services;
    }
}