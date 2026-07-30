using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Abstractions.Ai.Pipeline;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai.Memory;

/// <summary>Durable conversation ownership and lifecycle metadata.</summary>
public sealed class AeroAiConversationDocument : SableDocument
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public AeroAiAudience Audience { get; set; }
    public AeroAiPrincipalKind PrincipalKind { get; set; }
    public long PrincipalId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long LastMessageSequence { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset ModifiedOn { get; set; }
    public DateTimeOffset? ArchivedOn { get; set; }
}

/// <summary>One bounded message owned by a scoped conversation.</summary>
public sealed class AeroAiConversationMessageDocument : SableDocument
{
    public long ConversationId { get; set; }
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public AeroAiAudience Audience { get; set; }
    public AeroAiPrincipalKind PrincipalKind { get; set; }
    public long PrincipalId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public AeroCmsAssistantRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedOn { get; set; }
}

/// <summary>
/// A principal-private memory written only through an explicit confirmation boundary.
/// </summary>
public sealed class AeroAiExplicitMemoryDocument : SableDocument
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public AeroAiAudience Audience { get; set; }
    public AeroAiPrincipalKind PrincipalKind { get; set; }
    public long PrincipalId { get; set; }
    public string Culture { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long? SourceConversationId { get; set; }
    public long? SourceMessageId { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset ModifiedOn { get; set; }
}

internal static class AeroAiMemoryConstants
{
    public const int MaximumConversationListTake = 50;
    public const int MaximumConversationTitleCharacters = 120;
}
