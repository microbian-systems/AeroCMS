using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Core.Railway;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Programmatic MCP delegates that route every call through one authorized executor.</summary>
internal static class AeroCmsMcpToolBridge
{
    public static Task<string> CurrentSiteAsync(IServiceProvider services, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.CurrentSiteTool, new { }, ct);

    public static Task<string> ListPagesAsync(IServiceProvider services, int take, int skip, string? search, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.PagesListTool, new { take, skip, search }, ct);

    public static Task<string> GetPageAsync(IServiceProvider services, string id, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.PageGetTool, new { id }, ct);

    public static Task<string> CreatePageAsync(
        IServiceProvider services,
        string title,
        string slug,
        string? summary,
        string? rendererId,
        string? source,
        string? parentId,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.PageCreateTool, new
        {
            title, slug, summary, rendererId, source, parentId
        }, ct);

    public static Task<string> ListPostsAsync(IServiceProvider services, int take, int skip, string? search, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.PostsListTool, new { take, skip, search }, ct);

    public static Task<string> GetPostAsync(IServiceProvider services, string id, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.PostGetTool, new { id }, ct);

    public static Task<string> CreatePostAsync(
        IServiceProvider services,
        string title,
        string slug,
        string? excerpt,
        string? markdown,
        string? culture,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.PostCreateTool, new
        {
            title, slug, excerpt, markdown, culture
        }, ct);

    public static Task<string> ListDocsAsync(IServiceProvider services, int take, int skip, string? search, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.DocsListTool, new { take, skip, search }, ct);

    public static Task<string> GetDocAsync(IServiceProvider services, string id, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.DocGetTool, new { id }, ct);

    public static Task<string> CreateDocAsync(
        IServiceProvider services,
        string title,
        string slug,
        string? summary,
        string? markdown,
        string? culture,
        string? parentId,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.DocCreateTool, new
        {
            title, slug, summary, markdown, culture, parentId
        }, ct);

    public static Task<string> ListContentTypesAsync(IServiceProvider services, int take, int skip, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentTypesListTool, new { take, skip }, ct);

    public static Task<string> GetContentTypeAsync(IServiceProvider services, string alias, CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentTypeGetTool, new { alias }, ct);

    public static Task<string> CreateContentTypeAsync(
        IServiceProvider services,
        string alias,
        string name,
        string fieldsJson,
        string? description,
        string? category,
        string? structure,
        int maximumDepth,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentTypeCreateTool, new
        {
            alias, name, fieldsJson, description, category, structure, maximumDepth
        }, ct);

    public static Task<string> ListContentItemsAsync(
        IServiceProvider services,
        string alias,
        int take,
        int skip,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentItemsListTool, new { alias, take, skip }, ct);

    public static Task<string> GetContentItemAsync(
        IServiceProvider services,
        string alias,
        string id,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentItemGetTool, new { alias, id }, ct);

    public static Task<string> CreateContentItemAsync(
        IServiceProvider services,
        string alias,
        string title,
        string slug,
        string fieldsJson,
        string? culture,
        string? parentId,
        int sortOrder,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentItemCreateTool, new
        {
            alias, title, slug, fieldsJson, culture, parentId, sortOrder
        }, ct);

    public static Task<string> GetContentHierarchyAsync(
        IServiceProvider services,
        string alias,
        string? culture,
        string? traversal,
        string? rootId,
        int maximumDepth,
        int maximumItems,
        bool includeDrafts,
        CancellationToken ct) =>
        ExecuteAsync(services, AeroCmsToolExecutor.ContentHierarchyGetTool, new
        {
            alias, culture, traversal, rootId, maximumDepth, maximumItems, includeDrafts
        }, ct);

    private static async Task<string> ExecuteAsync(
        IServiceProvider services,
        string toolName,
        object arguments,
        CancellationToken cancellationToken)
    {
        var contextFactory = services.GetRequiredService<AeroCmsMcpInvocationContextFactory>();
        var contextResult = await contextFactory.CreateAsync(cancellationToken);
        if (contextResult is not Result<AeroCmsToolExecutionContext>.Ok context)
            return Error("Tool invocation was not authorized.");

        var executor = services.GetRequiredService<IAeroCmsToolExecutor>();
        var result = await executor.ExecuteAsync(
            toolName,
            JsonSerializer.SerializeToElement(arguments),
            context.Value,
            cancellationToken);
        return result is Result<AeroCmsToolResult>.Ok ok
            ? ok.Value.Json
            : Error(result is Result<AeroCmsToolResult>.Failure failure
                ? failure.Error.ToString()
                : "Tool invocation failed.");
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = message.Length <= 500 ? message : "Tool invocation failed." });
}
