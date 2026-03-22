using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Blog;

public sealed class BlogModule : AeroModuleBase
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
        await base.RunAsync(app);

        var blogService = app.ServiceProvider.GetRequiredService<IBlogPostContentService>();
        app.MapGet("/blog/{slug}", async (string slug, CancellationToken ct) =>
        {
            var post = await blogService.FindBySlugAsync(slug, ct);
            return post is not null ? Results.Ok(post.Body) : Results.NotFound();
        });
    }
}
