using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Disposable, provenance-rich full-text and vector projection for one authorized source chunk.
/// </summary>
public sealed class AeroAiKnowledgeChunkDocument : SableDocument
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public AeroAiAudience Audience { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public long SourceId { get; set; }
    public string SourceUri { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public long SourceRevision { get; set; }
    public int ChunkRevision { get; set; }
    public int SearchSchemaVersion { get; set; } = AeroAiKnowledgeConstants.SchemaVersion;
    public AeroAiFieldExposure FieldExposure { get; set; }
    public bool IsPublished { get; set; }
    public bool IncludeInSearch { get; set; }
    public bool IncludeInPublicAi { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset GeneratedOn { get; set; }
    public string? EmbeddingModelId { get; set; }
    public int? EmbeddingDimensions { get; set; }
    public float[]? Embedding { get; set; }
}
