using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Search;

public interface IContentEmbeddingGenerator
{
    string ModelId { get; }
    int Dimensions { get; }
    bool IsAvailable { get; }

    Task<Result<float[]>> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableContentEmbeddingGenerator : IContentEmbeddingGenerator
{
    public string ModelId => "unavailable";
    public int Dimensions => ContentSearchConstants.VectorDimensions;
    public bool IsAvailable => false;

    public Task<Result<float[]>> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Result<float[]>>(
            Aero.Core.AeroError.ValidationError(
                ["Semantic content search requires a configured embedding generator."]));
}
