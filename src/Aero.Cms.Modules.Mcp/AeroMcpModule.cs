using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Security;
using Aero.Cms.Core;
using Aero.Cms.Modules.AiAssistant;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Aero.Cms.Modules.RateLimiting;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Hosts the authenticated, site-scoped Streamable HTTP MCP server.</summary>
[Module(nameof(AeroMcpModule))]
public sealed class AeroMcpModule : AeroWebModule
{
    public override string Name => nameof(AeroMcpModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override string Description => "Authenticated site-scoped MCP tools and in-process assistant integration.";
    public override IReadOnlyList<string> Dependencies =>
        ["AiAssistantModule", nameof(RateLimitingModule), "SecurityModule", "SitesModule", "PagesModule", "PostsModule", "DocsModule", "ContentModule"];
    public override IReadOnlyList<string> Category => ["ai", "tools"];
    public override IReadOnlyList<string> Tags => ["ai", "mcp", "manager"];

    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.McpTransport,
            "McpTransport",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 120,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.McpManagement,
            "McpManagement",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 10,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroApplicationFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.McpRead,
            "McpRead",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 120,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroApplicationFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.McpWrite,
            "McpWrite",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 20,
                WindowSeconds = 60,
                QueueLimit = 0
            });
        services.AddAeroApplicationFixedWindowRateLimitPolicy(
            config,
            AeroRateLimitPolicyNames.McpDestructive,
            "McpDestructive",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 5,
                WindowSeconds = 60,
                QueueLimit = 0
            });

        services.AddHttpContextAccessor();
        services.AddScoped<AeroCmsMcpInvocationContextFactory>();
        services.AddScoped<IAeroCmsToolExecutor, AeroCmsToolExecutor>();
        services.AddScoped<IAeroCmsAssistantToolProvider, AeroCmsAssistantToolProvider>();

        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools(CreateTools());
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapMcp("/mcp")
            .RequireAuthorization(AeroApiKeyAuthenticationDefaults.McpPolicy)
            .RequireRateLimiting(AeroRateLimitPolicyNames.McpTransport);
        builder.MapAeroMcpApiKeyEndpoints();
        builder.MapAeroCmsAssistantEndpoints();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<McpServerTool> CreateTools() =>
    [
        Tool((CurrentSiteToolDelegate)AeroCmsMcpToolBridge.CurrentSiteAsync, AeroCmsToolExecutor.CurrentSiteTool, "Returns the selected site.", true, true),
        Tool((ListToolDelegate)AeroCmsMcpToolBridge.ListPagesAsync, AeroCmsToolExecutor.PagesListTool, "Lists pages. take is bounded to 25.", true, true),
        Tool((GetToolDelegate)AeroCmsMcpToolBridge.GetPageAsync, AeroCmsToolExecutor.PageGetTool, "Gets one page. Snowflake IDs are decimal strings.", true, true),
        Tool((CreatePageToolDelegate)AeroCmsMcpToolBridge.CreatePageAsync, AeroCmsToolExecutor.PageCreateTool, "Creates a draft page.", false, false),
        Tool((ListToolDelegate)AeroCmsMcpToolBridge.ListPostsAsync, AeroCmsToolExecutor.PostsListTool, "Lists blog posts. take is bounded to 25.", true, true),
        Tool((GetToolDelegate)AeroCmsMcpToolBridge.GetPostAsync, AeroCmsToolExecutor.PostGetTool, "Gets one blog post.", true, true),
        Tool((CreatePostToolDelegate)AeroCmsMcpToolBridge.CreatePostAsync, AeroCmsToolExecutor.PostCreateTool, "Creates a draft blog post.", false, false),
        Tool((ListToolDelegate)AeroCmsMcpToolBridge.ListDocsAsync, AeroCmsToolExecutor.DocsListTool, "Lists documentation entries.", true, true),
        Tool((GetToolDelegate)AeroCmsMcpToolBridge.GetDocAsync, AeroCmsToolExecutor.DocGetTool, "Gets one documentation entry.", true, true),
        Tool((CreateDocToolDelegate)AeroCmsMcpToolBridge.CreateDocAsync, AeroCmsToolExecutor.DocCreateTool, "Creates a draft documentation entry.", false, false),
        Tool((ListContentTypesToolDelegate)AeroCmsMcpToolBridge.ListContentTypesAsync, AeroCmsToolExecutor.ContentTypesListTool, "Lists content types.", true, true),
        Tool((GetContentTypeToolDelegate)AeroCmsMcpToolBridge.GetContentTypeAsync, AeroCmsToolExecutor.ContentTypeGetTool, "Gets one content type by alias.", true, true),
        Tool((CreateContentTypeToolDelegate)AeroCmsMcpToolBridge.CreateContentTypeAsync, AeroCmsToolExecutor.ContentTypeCreateTool, "Creates a content type. fieldsJson is an array of field definitions.", false, false),
        Tool((ListContentItemsToolDelegate)AeroCmsMcpToolBridge.ListContentItemsAsync, AeroCmsToolExecutor.ContentItemsListTool, "Lists items for one content type.", true, true),
        Tool((GetContentItemToolDelegate)AeroCmsMcpToolBridge.GetContentItemAsync, AeroCmsToolExecutor.ContentItemGetTool, "Gets one content item.", true, true),
        Tool((CreateContentItemToolDelegate)AeroCmsMcpToolBridge.CreateContentItemAsync, AeroCmsToolExecutor.ContentItemCreateTool, "Creates a draft content item. fieldsJson is an object.", false, false),
        Tool((GetHierarchyToolDelegate)AeroCmsMcpToolBridge.GetContentHierarchyAsync, AeroCmsToolExecutor.ContentHierarchyGetTool, "Gets a bounded hierarchy projection.", true, true)
    ];

    private static McpServerTool Tool(
        Delegate callback,
        string name,
        string description,
        bool readOnly,
        bool idempotent) =>
        McpServerTool.Create(callback, new McpServerToolCreateOptions
        {
            Name = name,
            Description = description,
            ReadOnly = readOnly,
            Destructive = false,
            Idempotent = idempotent,
            OpenWorld = false
        });

    private delegate Task<string> CurrentSiteToolDelegate(IServiceProvider services, CancellationToken cancellationToken = default);
    private delegate Task<string> ListToolDelegate(IServiceProvider services, int take = 10, int skip = 0, string? search = null, CancellationToken cancellationToken = default);
    private delegate Task<string> GetToolDelegate(IServiceProvider services, string id, CancellationToken cancellationToken = default);
    private delegate Task<string> CreatePageToolDelegate(IServiceProvider services, string title, string slug, string? summary = null, string? rendererId = null, string? source = null, string? parentId = null, CancellationToken cancellationToken = default);
    private delegate Task<string> CreatePostToolDelegate(IServiceProvider services, string title, string slug, string? excerpt = null, string? markdown = null, string? culture = null, CancellationToken cancellationToken = default);
    private delegate Task<string> CreateDocToolDelegate(IServiceProvider services, string title, string slug, string? summary = null, string? markdown = null, string? culture = null, string? parentId = null, CancellationToken cancellationToken = default);
    private delegate Task<string> ListContentTypesToolDelegate(IServiceProvider services, int take = 10, int skip = 0, CancellationToken cancellationToken = default);
    private delegate Task<string> GetContentTypeToolDelegate(IServiceProvider services, string alias, CancellationToken cancellationToken = default);
    private delegate Task<string> CreateContentTypeToolDelegate(IServiceProvider services, string alias, string name, string fieldsJson, string? description = null, string? category = null, string? structure = null, int maximumDepth = 8, CancellationToken cancellationToken = default);
    private delegate Task<string> ListContentItemsToolDelegate(IServiceProvider services, string alias, int take = 10, int skip = 0, CancellationToken cancellationToken = default);
    private delegate Task<string> GetContentItemToolDelegate(IServiceProvider services, string alias, string id, CancellationToken cancellationToken = default);
    private delegate Task<string> CreateContentItemToolDelegate(IServiceProvider services, string alias, string title, string slug, string fieldsJson, string? culture = null, string? parentId = null, int sortOrder = 0, CancellationToken cancellationToken = default);
    private delegate Task<string> GetHierarchyToolDelegate(IServiceProvider services, string alias, string? culture = null, string? traversal = null, string? rootId = null, int maximumDepth = 6, int maximumItems = 100, bool includeDrafts = false, CancellationToken cancellationToken = default);
}
