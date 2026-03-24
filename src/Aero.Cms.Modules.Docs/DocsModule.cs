using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsModule : AeroModuleBase
{
    public override string Name => nameof(DocsModule);
    public override string Version => "1.0.0";
    public override string Author => "Aero CMS Team";
    public override short Order => 100;

    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["documentation"];
    public override IReadOnlyList<string> Tags => ["docs", "markdown"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddSingleton<global::Marten.IConfigureMarten, DocsMartenConfiguration>();
        services.AddScoped<IDocsService, DocsService>();
    }

    public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
        await Task.CompletedTask;
    }
}
