using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Mcp;

public class AeroMcpModule : AeroModuleBase
{
    public override string Name => nameof(AeroMcpModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;

    public override string Description =>
        "an MCP server for your aero instance. it can answer questions based on what you allow it to";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["ai", "tools"];
    public override IReadOnlyList<string> Tags => ["ai", "mcp"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {

    }
}