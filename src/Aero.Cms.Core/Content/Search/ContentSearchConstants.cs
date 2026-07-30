namespace Aero.Cms.Core.Content.Search;

public static class ContentSearchConstants
{
    public const string AnalyzerName = "aero_content";
    public const int SchemaVersion = 1;
    public const int VectorDimensions = 384;
    public const int MaximumQueryLength = 256;
    public const int MaximumPublicTake = 50;
    public const int MaximumInternalTake = 200;
    public const int MaximumSkip = 10_000;
    public const int MaximumCandidates = 500;
    public const int MaximumFacetsPerItem = 500;
}
