using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai.Memory;

/// <summary>Identifies the durable identity boundary that owns AI state.</summary>
public enum AeroAiPrincipalKind
{
    ManagerUser = 0,
    Member = 1
}

/// <summary>
/// Server-derived ownership scope for conversation history and explicit long-term memory.
/// </summary>
public sealed record AeroAiMemoryScope(
    long TenantId,
    long SiteId,
    AeroAiAudience Audience,
    AeroAiPrincipalKind PrincipalKind,
    long PrincipalId,
    string Culture);

/// <summary>A server-owned conversation and the bounded history to send to the provider.</summary>
public sealed record AeroAiConversationTurn(
    long ConversationId,
    IReadOnlyList<AeroCmsAssistantMessage> Messages);

/// <summary>
/// Persists conversation turns while rechecking the full ownership scope on every operation.
/// </summary>
public interface IAeroAiConversationStore
{
    Task<Result<AeroAiConversationTurn>> BeginTurnAsync(
        AeroAiMemoryScope scope,
        long? conversationId,
        IReadOnlyList<AeroCmsAssistantMessage> requestMessages,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> AppendAssistantMessageAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        string content,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>> ListAsync(
        AeroAiMemoryScope scope,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<Result<AeroCmsAssistantConversation>> GetAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        CancellationToken cancellationToken = default);
}

/// <summary>An explicitly confirmed, principal-private long-term memory.</summary>
public sealed record AeroAiExplicitMemory(
    long Id,
    string Label,
    string Content,
    long? SourceConversationId,
    long? SourceMessageId,
    DateTimeOffset CreatedOn,
    DateTimeOffset ModifiedOn);

/// <summary>Input for a user-confirmed long-term memory write.</summary>
public sealed record AeroAiExplicitMemoryWrite(
    string Label,
    string Content,
    long? SourceConversationId = null,
    long? SourceMessageId = null,
    long? MemoryId = null);

/// <summary>Shared bounds for explicit, user-confirmed long-term memory.</summary>
public static class AeroAiMemoryLimits
{
    public const int MaximumExplicitMemories = 100;
    public const int MaximumMemoryLabelCharacters = 120;
    public const int MaximumMemoryContentCharacters = 2_000;
    public const int MaximumMemoryListTake = 50;
}

/// <summary>
/// Stores only explicit memories. Implementations must not infer or promote memories automatically.
/// </summary>
public interface IAeroAiExplicitMemoryStore
{
    Task<Result<AeroAiExplicitMemory>> SaveAsync(
        AeroAiMemoryScope scope,
        AeroAiExplicitMemoryWrite memory,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AeroAiExplicitMemory>>> ListAsync(
        AeroAiMemoryScope scope,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(
        AeroAiMemoryScope scope,
        long memoryId,
        CancellationToken cancellationToken = default);
}
