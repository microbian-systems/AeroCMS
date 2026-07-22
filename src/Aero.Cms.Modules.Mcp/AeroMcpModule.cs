using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Hosts the authenticated Streamable HTTP MCP server and assistant HTTP boundary.</summary>
[Module(nameof(AeroMcpModule))]
public sealed class AeroMcpModule : AeroWebModule
{
    public override string Name => nameof(AeroMcpModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override string Description => "Authenticated, site-scoped read-only MCP tools and assistant transport.";
    public override IReadOnlyList<string> Dependencies =>
        ["AiAssistantModule", "SitesModule", "PagesModule"];
    public override IReadOnlyList<string> Category => ["ai", "tools"];
    public override IReadOnlyList<string> Tags => ["ai", "mcp", "manager"];

    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AeroCmsMcpInvocationContextFactory>();
        services.AddScoped<IAeroCmsReadOnlyToolExecutor, AeroCmsReadOnlyToolExecutor>();

        var tools = CreateTools();
        services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithTools(tools);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapMcp("/mcp")
            .RequireAuthorization()
            .RequireAuthorization("site:read");
        builder.MapAeroCmsAssistantEndpoints();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<McpServerTool> CreateTools()
    {
        return
        [
            McpServerTool.Create(
                (CurrentSiteToolDelegate)AeroCmsMcpToolBridge.CurrentSiteAsync,
                ToolOptions(
                    AeroCmsReadOnlyToolExecutor.CurrentSiteTool,
                    "Returns the authenticated manager's currently selected AeroCMS site.")),
            McpServerTool.Create(
                (ListPagesToolDelegate)AeroCmsMcpToolBridge.ListPagesAsync,
                ToolOptions(
                    AeroCmsReadOnlyToolExecutor.PagesListTool,
                    "Lists at most 25 page summaries from the authenticated manager's selected site.")),
            McpServerTool.Create(
                (GetPageToolDelegate)AeroCmsMcpToolBridge.GetPageAsync,
                ToolOptions(
                    AeroCmsReadOnlyToolExecutor.PageGetTool,
                    "Gets one positive page identifier from the authenticated manager's selected site."))
        ];
    }

    private static McpServerToolCreateOptions ToolOptions(string name, string description) => new()
    {
        Name = name,
        Description = description,
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    };

    private delegate Task<string> CurrentSiteToolDelegate(
        IServiceProvider services,
        CancellationToken cancellationToken = default);

    private delegate Task<string> ListPagesToolDelegate(
        IServiceProvider services,
        int take = 10,
        int skip = 0,
        CancellationToken cancellationToken = default);

    private delegate Task<string> GetPageToolDelegate(
        IServiceProvider services,
        long id,
        CancellationToken cancellationToken = default);
}
