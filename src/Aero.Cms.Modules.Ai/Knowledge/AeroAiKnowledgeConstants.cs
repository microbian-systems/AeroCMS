namespace Aero.Cms.Modules.Ai.Knowledge;

internal static class AeroAiKnowledgeConstants
{
    public const string AnalyzerName = "aero_ai_knowledge";
    public const int SchemaVersion = 1;
    public const int VectorDimensions = 384;
    public const int MaximumQueryLength = 512;
    public const int MaximumTake = 20;
    public const int MaximumCandidates = 200;
    public const int MaximumChunksPerSource = 128;
    public const int MaximumSectionCharacters = 250_000;
    public const int MaximumChunkCharacters = 4_000;
    public const int ChunkOverlapCharacters = 200;
}
