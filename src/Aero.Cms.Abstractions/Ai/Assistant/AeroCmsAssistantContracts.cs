using System.Security.Claims;
using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai.Assistant;

/// <summary>Names the only conversation roles accepted from the manager client.</summary>
public enum AeroCmsAssistantRole
{
    User = 0,
    Assistant = 1
}

/// <summary>A bounded, public conversation message without provider or system-prompt controls.</summary>
public sealed record AeroCmsAssistantMessage(AeroCmsAssistantRole Role, string Content);

/// <summary>A stateless assistant request. The server owns provider, model, prompt, and tool policy.</summary>
public sealed record AeroCmsAssistantRequest(IReadOnlyList<AeroCmsAssistantMessage> Messages);

/// <summary>A completed assistant response.</summary>
public sealed record AeroCmsAssistantResponse(string Text, string CorrelationId);

/// <summary>Names events emitted by assistant streaming endpoints.</summary>
public enum AeroCmsAssistantEventKind
{
    Metadata = 0,
    Delta = 1,
    Complete = 2,
    Error = 3
}

/// <summary>A bounded event emitted during an assistant response.</summary>
public sealed record AeroCmsAssistantEvent(
    AeroCmsAssistantEventKind Kind,
    string? Data = null,
    string? CorrelationId = null);

/// <summary>Central protocol limits enforced independently by clients and services.</summary>
public static class AeroCmsAssistantLimits
{
    public const int MaxMessages = 20;
    public const int MaxUserMessageCharacters = 8_000;
    public const int MaxConversationCharacters = 32_000;
    public const int MaxOutputCharacters = 32_000;
    public const int MaxEventCharacters = 64_000;
}

/// <summary>Runs provider-backed, stateless manager conversations.</summary>
public interface IAeroCmsAssistantService
{
    Task<Result<AeroCmsAssistantResponse>> CompleteAsync(
        AeroCmsAssistantRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Result<IAsyncEnumerable<AeroCmsAssistantEvent>>> StreamAsync(
        AeroCmsAssistantRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Typed browser/server client for the authenticated assistant endpoints.</summary>
public interface IMcpAssistantHttpClient
{
    Task<Result<AeroCmsAssistantResponse>> CompleteAsync(
        AeroCmsAssistantRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IAsyncEnumerable<AeroCmsAssistantEvent>>> StreamAsync(
        AeroCmsAssistantRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Immutable authorization context supplied to one read-only tool invocation.</summary>
public sealed record AeroCmsToolExecutionContext(
    ClaimsPrincipal Principal,
    long UserId,
    long SiteId,
    long TenantId,
    string CorrelationId);

/// <summary>Describes a read-only CMS tool exposed through MCP.</summary>
public sealed record AeroCmsReadOnlyToolDescriptor(string Name, string Description);

/// <summary>A serialized, bounded tool result.</summary>
public sealed record AeroCmsReadOnlyToolResult(string Json);

/// <summary>Single executor boundary shared by every MCP read-only tool.</summary>
public interface IAeroCmsReadOnlyToolExecutor
{
    IReadOnlyList<AeroCmsReadOnlyToolDescriptor> Tools { get; }

    Task<Result<AeroCmsReadOnlyToolResult>> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
