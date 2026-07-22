using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Core.Railway;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Programmatic MCP delegates that route every call through one authorized executor.</summary>
internal static class AeroCmsMcpToolBridge
{
    public static Task<string> CurrentSiteAsync(IServiceProvider services, CancellationToken cancellationToken)
        => ExecuteAsync(services, AeroCmsReadOnlyToolExecutor.CurrentSiteTool, EmptyArguments(), cancellationToken);

    public static Task<string> ListPagesAsync(
        IServiceProvider services,
        int take = 10,
        int skip = 0,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            services,
            AeroCmsReadOnlyToolExecutor.PagesListTool,
            JsonSerializer.SerializeToElement(new { take, skip }),
            cancellationToken);

    public static Task<string> GetPageAsync(
        IServiceProvider services,
        long id,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            services,
            AeroCmsReadOnlyToolExecutor.PageGetTool,
            JsonSerializer.SerializeToElement(new { id }),
            cancellationToken);

    private static async Task<string> ExecuteAsync(
        IServiceProvider services,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var contextFactory = services.GetRequiredService<AeroCmsMcpInvocationContextFactory>();
        var contextResult = await contextFactory.CreateAsync(cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure)
            return "{\"error\":\"Tool invocation was not authorized.\"}";

        var executor = services.GetRequiredService<IAeroCmsReadOnlyToolExecutor>();
        var result = await executor.ExecuteAsync(
            toolName,
            arguments,
            ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value,
            cancellationToken);
        return result is Result<AeroCmsReadOnlyToolResult>.Ok ok
            ? ok.Value.Json
            : "{\"error\":\"Tool invocation failed.\"}";
    }

    private static JsonElement EmptyArguments()
        => JsonSerializer.SerializeToElement(new { });
}
