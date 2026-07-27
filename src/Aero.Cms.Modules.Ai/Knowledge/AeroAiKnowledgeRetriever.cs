using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Core.Content.Search;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>Runs bounded full-text or hybrid retrieval with scope filters inside each search query.</summary>
public sealed class AeroAiKnowledgeRetriever(
    IDocumentSession session,
    IContentEmbeddingGenerator embeddingGenerator)
    : IAeroAiKnowledgeRetriever
{
    public async Task<Result<IReadOnlyList<AeroAiKnowledgeMatch>>> SearchAsync(
        AeroAiKnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(query);
        if (validationError is not null)
            return validationError;

        var corpusAudience = query.Audience == AeroAiAudience.Manager
            ? AeroAiAudience.Manager
            : AeroAiAudience.Public;
        var search = session.Search<AeroAiKnowledgeChunkDocument>()
            .MatchText(chunk => (object)chunk.FullText, query.Query.Trim())
            .Where(chunk =>
                chunk.TenantId == query.TenantId
                && chunk.SiteId == query.SiteId
                && chunk.Audience == corpusAudience
                && chunk.Culture == query.Culture
                && chunk.IncludeInSearch)
            .Take(Math.Clamp(query.Take, 1, AeroAiKnowledgeConstants.MaximumTake))
            .Candidates(AeroAiKnowledgeConstants.MaximumCandidates);

        List<AeroAiKnowledgeChunkDocument> documents;
        if (embeddingGenerator.IsAvailable)
        {
            if (embeddingGenerator.Dimensions != AeroAiKnowledgeConstants.VectorDimensions)
            {
                return AeroError.ValidationError(
                    [$"The configured embedding generator must emit {AeroAiKnowledgeConstants.VectorDimensions} dimensions."]);
            }

            var generated = await embeddingGenerator.GenerateAsync(
                query.Query.Trim(),
                cancellationToken);
            if (generated is not Result<float[]>.Ok success)
                return ((Result<float[]>.Failure)generated).Error;
            if (success.Value.Length != AeroAiKnowledgeConstants.VectorDimensions)
            {
                return AeroError.ValidationError(
                    [$"The embedding generator returned {success.Value.Length} dimensions; " +
                     $"{AeroAiKnowledgeConstants.VectorDimensions} are required."]);
            }

            documents = await search
                .WithVector(chunk => chunk.Embedding!, success.Value)
                .FuseAsync(
                    rrfK: 60,
                    rrfLimit: AeroAiKnowledgeConstants.MaximumCandidates,
                    cancellationToken);
        }
        else
        {
            documents = await search.ToListAsync(cancellationToken);
        }

        return documents
            .Take(Math.Clamp(query.Take, 1, AeroAiKnowledgeConstants.MaximumTake))
            .Select(document => new AeroAiKnowledgeMatch(
                document.Id,
                document.SourceKind,
                document.SourceId,
                document.SourceUri,
                document.Culture,
                document.Title,
                document.Section,
                document.Content,
                document.SourceRevision,
                document.ChunkRevision,
                document.ContentHash))
            .ToArray();
    }

    private static AeroError? Validate(AeroAiKnowledgeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TenantId <= 0 || query.SiteId <= 0)
            return AeroError.ValidationError(["A valid tenant and site scope is required."]);
        if (query.Audience is not (AeroAiAudience.Public or AeroAiAudience.Member or AeroAiAudience.Manager))
            return AeroError.ForbiddenError("This audience cannot retrieve the AI knowledge corpus.");
        if (string.IsNullOrWhiteSpace(query.Culture))
            return AeroError.ValidationError(["Culture is required."]);
        if (string.IsNullOrWhiteSpace(query.Query))
            return AeroError.ValidationError(["Search query is required."]);
        if (query.Query.Length > AeroAiKnowledgeConstants.MaximumQueryLength)
            return AeroError.ValidationError(["Search query exceeds the bounded length."]);
        if (query.Take is < 1 or > AeroAiKnowledgeConstants.MaximumTake)
            return AeroError.ValidationError(
                [$"Take must be between 1 and {AeroAiKnowledgeConstants.MaximumTake}."]);
        return null;
    }
}
