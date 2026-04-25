using Aero.Cms.Core.Http.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThrowGuard;


namespace Aero.Cms.Core.Extensions;

public static class AeroHttpClientExtensions
{
    public static IServiceCollection AddAeroHttpClients(this IServiceCollection services, IConfiguration config)
    {
        var url = config["ApiSettings:BaseUrl"]
            ?? config["AeroHttpClientBaseAddress"]
            ;
  
        ThrowGuard.Throw.IfNullOrEmpty(url, msg: "httpclient url must be valid", argName: nameof(url));
        var uri = new Uri(url);

        services.ConfigureHttpClientDefaults(b =>
        {
            b.AddStandardResilienceHandler();
        });

        services.AddHttpClient<IBlogHttpClient, BlogHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ICategoriesHttpClient, CategoriesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IDashboardHttpClient, DashboardHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IFilesHttpClient, FilesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IMediaHttpClient, MediaHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IModulesHttpClient, ModulesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<INavigationsHttpClient, NavigationsHttpClient>(c => c.BaseAddress = uri); 
        services.AddHttpClient<IPagesHttpClient, PagesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IProfileHttpClient, ProfileHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ISettingsHttpClient, SettingsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<ITagsHttpClient, TagsHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IThemesHttpClient, ThemesHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IUsersHttpClient, UsersHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IBlocksHttpClient, BlocksHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IPublishHttpClient, PublishHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IPreviewHttpClient, PreviewHttpClient>(c => c.BaseAddress = uri);
        services.AddHttpClient<IDocsHttpClient, DocsHttpClient>(c => c.BaseAddress = uri);

        return services;
    }
}
