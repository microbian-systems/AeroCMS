using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Records which immutable Git documentation snapshot is fully represented by the manager projection.
/// </summary>
public sealed class AeroManagerDocumentationCorpusStateDocument : SableDocument, IVersioned
{
    public string CorpusId { get; set; } = AeroDocumentationKnowledgeConstants.CorpusId;
    public int SchemaVersion { get; set; }
    public int SearchSchemaVersion { get; set; }
    public string GitCommit { get; set; } = string.Empty;
    public string CorpusHash { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public int EmbeddedChunkCount { get; set; }
    public string? EmbeddingModelId { get; set; }
    public int? EmbeddingDimensions { get; set; }
    public bool EmbeddingsReady { get; set; }
    public DateTimeOffset ReconciledOn { get; set; }
    public long Version { get; set; }
}

internal static class AeroDocumentationKnowledgeConstants
{
    public const string CorpusId = "aerocms-git-docs";
    public const long CorpusStateId = 1;
}
