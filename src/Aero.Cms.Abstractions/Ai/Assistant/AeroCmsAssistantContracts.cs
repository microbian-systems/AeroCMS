using System.Security.Claims;
using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Memory;
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

/// <summary>
/// A bounded assistant request. The optional conversation identifier is only a lookup key; the
/// server re-establishes its tenant, site, audience, and principal ownership before loading history.
/// </summary>
public sealed record AeroCmsAssistantRequest(
    IReadOnlyList<AeroCmsAssistantMessage> Messages,
    long? ConversationId = null);

/// <summary>A security-scoped CMS source used to ground an assistant response.</summary>
public sealed record AeroCmsAssistantCitation(
    string Id,
    string SourceKind,
    string SourceId,
    string SourceUri,
    string Title,
    string Section);

/// <summary>One durable conversation owned by the current site and principal.</summary>
public sealed record AeroCmsAssistantConversationSummary(
    long ConversationId,
    string Title,
    DateTimeOffset CreatedOn,
    DateTimeOffset ModifiedOn);

/// <summary>A bounded durable conversation transcript.</summary>
public sealed record AeroCmsAssistantConversation(
    long ConversationId,
    string Title,
    IReadOnlyList<AeroCmsAssistantMessage> Messages,
    DateTimeOffset CreatedOn,
    DateTimeOffset ModifiedOn);

/// <summary>A completed assistant response.</summary>
public sealed record AeroCmsAssistantResponse(
    string Text,
    string CorrelationId,
    long ConversationId = 0,
    IReadOnlyList<AeroCmsAssistantCitation>? Citations = null);

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
    string? CorrelationId = null,
    long? ConversationId = null,
    IReadOnlyList<AeroCmsAssistantCitation>? Citations = null);

/// <summary>Central protocol limits enforced independently by clients and services.</summary>
public static class AeroCmsAssistantLimits
{
    public const int MaxMessages = 20;
    public const int MaxUserMessageCharacters = 8_000;
    public const int MaxConversationCharacters = 32_000;
    public const int MaxOutputCharacters = 32_000;
    public const int MaxEventCharacters = 64_000;
    public const int MaxStoredMessages = 200;
}

/// <summary>Runs provider-backed, server-owned manager conversations.</summary>
public interface IAeroCmsAssistantService
{
    Task<Result<AeroCmsAssistantResponse>> CompleteAsync(
        AeroCmsAssistantRequest request,
        AeroCmsToolExecutionContext executionContext,
        CancellationToken cancellationToken = default);

    Task<Result<IAsyncEnumerable<AeroCmsAssistantEvent>>> StreamAsync(
        AeroCmsAssistantRequest request,
        AeroCmsToolExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Server-derived scope for a public or authenticated member assistant request.
/// </summary>
public sealed record AeroCmsSiteAssistantContext(
    Aero.Cms.Abstractions.Ai.Pipeline.AeroAiAudience Audience,
    ClaimsPrincipal Principal,
    long PrincipalId,
    long TenantId,
    long SiteId,
    string Culture,
    string CorrelationId);

/// <summary>
/// Runs public-corpus-only site conversations without exposing manager tools or internal knowledge.
/// </summary>
public interface IAeroCmsSiteAssistantService
{
    Task<Result<AeroCmsAssistantResponse>> CompleteAsync(
        AeroCmsAssistantRequest request,
        AeroCmsSiteAssistantContext context,
        CancellationToken cancellationToken = default);

    Task<Result<IAsyncEnumerable<AeroCmsAssistantEvent>>> StreamAsync(
        AeroCmsAssistantRequest request,
        AeroCmsSiteAssistantContext context,
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

    Task<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>> ListConversationsAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<Result<AeroCmsAssistantConversation>> GetConversationAsync(
        long conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteConversationAsync(
        long conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AeroAiExplicitMemory>>> ListMemoriesAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<Result<AeroAiExplicitMemory>> SaveMemoryAsync(
        AeroAiExplicitMemoryWrite memory,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteMemoryAsync(
        long memoryId,
        CancellationToken cancellationToken = default);
}

/// <summary>Immutable authorization context supplied to one CMS tool invocation.</summary>
public sealed record AeroCmsToolExecutionContext(
    ClaimsPrincipal Principal,
    long UserId,
    long SiteId,
    long TenantId,
    string CorrelationId);

/// <summary>Describes one explicitly registered, policy-scoped CMS tool.</summary>
public sealed record AeroCmsToolDescriptor(
    string Name,
    string Description,
    string RequiredPolicy,
    string PermissionDomain,
    char PermissionOperation,
    bool ReadOnly,
    bool Destructive,
    bool Idempotent);

/// <summary>A serialized, bounded tool result.</summary>
public sealed record AeroCmsToolResult(string Json);

/// <summary>Single application boundary shared by MCP and the in-process manager assistant.</summary>
public interface IAeroCmsToolExecutor
{
    IReadOnlyList<AeroCmsToolDescriptor> Tools { get; }

    Task<Result<IReadOnlyList<AeroCmsToolDescriptor>>> GetAuthorizedToolsAsync(
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<Result<AeroCmsToolResult>> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
