using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Modules.AiAssistant;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.AI;

namespace Aero.Cms.Modules.Mcp;

/// <summary>
/// Adapts the shared CMS executor into request-scoped in-process AI functions.
/// </summary>
internal sealed class AeroCmsAssistantToolProvider(
    AeroCmsMcpInvocationContextFactory contextFactory,
    IAeroCmsToolExecutor executor) : IAeroCmsAssistantToolProvider
{
    public async Task<Result<IReadOnlyList<AITool>>> CreateToolsAsync(
        CancellationToken cancellationToken = default)
    {
        var contextResult = await contextFactory.CreateAsync(cancellationToken);
        if (contextResult is Result<AeroCmsToolExecutionContext>.Failure failure)
            return failure.Error;
        var context = ((Result<AeroCmsToolExecutionContext>.Ok)contextResult).Value;

        Func<Task<string>> catalog = () => Task.FromResult(JsonSerializer.Serialize(
            executor.Tools.Select(tool => new
            {
                tool.Name,
                tool.Description,
                tool.ReadOnly,
                tool.RequiredPolicy
            })));
        Func<string, string, CancellationToken, Task<string>> execute =
            (toolName, argumentsJson, ct) => ExecuteAsync(
                toolName,
                argumentsJson,
                context,
                ct);

        return new List<AITool>
        {
            AIFunctionFactory.Create(
                catalog,
                "aero_cms_tool_catalog",
                "Lists the exact AeroCMS tool names, descriptions, mutation status, and required site policy.",
                JsonSerializerOptions.Web),
            AIFunctionFactory.Create(
                execute,
                "aero_cms_execute",
                "Executes one exact AeroCMS tool in the authenticated selected site. argumentsJson must be a JSON object. Inspect aero_cms_tool_catalog first when unsure.",
                JsonSerializerOptions.Web)
        };
    }

    private async Task<string> ExecuteAsync(
        string toolName,
        string argumentsJson,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        JsonElement arguments;
        try
        {
            arguments = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            if (arguments.ValueKind != JsonValueKind.Object)
                return Error("argumentsJson must contain one JSON object.");
        }
        catch (JsonException)
        {
            return Error("argumentsJson was not valid JSON.");
        }

        var result = await executor.ExecuteAsync(
            toolName,
            arguments,
            context,
            cancellationToken);
        return result is Result<AeroCmsToolResult>.Ok ok
            ? ok.Value.Json
            : Error(result is Result<AeroCmsToolResult>.Failure failure
                ? failure.Error.ToString()
                : "Tool invocation failed.");
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new
        {
            error = message.Length <= 500 ? message : "Tool invocation failed."
        });
}
