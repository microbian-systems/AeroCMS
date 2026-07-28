using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Rebuildable manager-only full-text and vector projection of curated AeroCMS documentation.
/// </summary>
public sealed class AeroManagerDocumentationChunkDocument : SableDocument
{
    public string CorpusId { get; set; } = AeroDocumentationKnowledgeConstants.CorpusId;
    public string TrustClass { get; set; } = string.Empty;
    public string SourceAudience { get; set; } = string.Empty;
    public long SourceId { get; set; }
    public string SourceUri { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public long SourceRevision { get; set; }
    public int ChunkRevision { get; set; }
    public int SearchSchemaVersion { get; set; } = AeroAiKnowledgeConstants.SchemaVersion;
    public string Title { get; set; } = string.Empty;
    public string FeatureArea { get; set; } = string.Empty;
    public string Maturity { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset GeneratedOn { get; set; }
    public string? EmbeddingModelId { get; set; }
    public int? EmbeddingDimensions { get; set; }
    public float[]? Embedding { get; set; }
}
