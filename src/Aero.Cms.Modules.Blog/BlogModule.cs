using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Blog;

public sealed class BlogModule : AeroModuleBase, IUiModule
{
    public override string Name => nameof(BlogModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [nameof(Aero.Cms.Modules.Pages.PagesModule)];
    public override IReadOnlyList<string> Category => ["content", "blog"];
    public override IReadOnlyList<string> Tags => ["content", "blog", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IBlogPostContentService, MartenBlogPostContentService>();
    }

    public override async Task RunAsync(IEndpointRouteBuilder app)
    {
        var scope = app.ServiceProvider.CreateAsyncScope();
        _ = scope.ServiceProvider.GetRequiredService<IBlogPostContentService>();

        app.MapRazorPages();
    }
}
