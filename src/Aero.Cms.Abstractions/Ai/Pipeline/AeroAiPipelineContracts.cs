using System.Security.Claims;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai.Pipeline;

/// <summary>Identifies the trust plane selected before AI work begins.</summary>
public enum AeroAiAudience
{
    Public = 0,
    Member = 1,
    Manager = 2,
    Mcp = 3
}

/// <summary>Identifies the bounded application operation that enters the AI pipeline.</summary>
public enum AeroAiOperation
{
    Assistant = 0,
    ContentEnhancement = 1,
    Translation = 2,
    Retrieval = 3,
    ToolInvocation = 4
}

/// <summary>
/// Server-derived request metadata shared by ordered AI pipeline stages.
/// Prompt and document content are deliberately kept out of this context.
/// </summary>
public sealed record AeroAiPipelineContext(
    AeroAiAudience Audience,
    AeroAiOperation Operation,
    ClaimsPrincipal Principal,
    long PrincipalId,
    long TenantId,
    long SiteId,
    string Culture,
    string CorrelationId,
    int InputItemCount,
    int InputCharacterCount,
    bool IsStreaming);

/// <summary>Invokes the next application stage or the terminal AI operation.</summary>
public delegate Task<Result<T>> AeroAiPipelineNext<T>(
    AeroAiPipelineContext context,
    CancellationToken cancellationToken);

/// <summary>
/// One ordered, independently testable AI request stage. A stage may stop the chain by returning
/// a failure, but it cannot broaden the authority contained in the server-derived context.
/// </summary>
public interface IAeroAiPipelineStage
{
    string Name { get; }

    int Order { get; }

    bool AppliesTo(AeroAiPipelineContext context);

    Task<Result<T>> InvokeAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> next,
        CancellationToken cancellationToken);
}

/// <summary>Composes the registered stages around one typed terminal operation.</summary>
public interface IAeroAiRequestPipeline
{
    Task<Result<T>> ExecuteAsync<T>(
        AeroAiPipelineContext context,
        AeroAiPipelineNext<T> terminal,
        CancellationToken cancellationToken = default);
}

/// <summary>Stable ordering bands for first-party and feature-contributed stages.</summary>
public static class AeroAiPipelineOrder
{
    public const int RequestNormalization = 100;
    public const int Scope = 200;
    public const int Authorization = 300;
    public const int Admission = 400;
    public const int InputSafety = 500;
    public const int Context = 600;
    public const int Retrieval = 700;
    public const int ToolCatalog = 800;
    public const int Provider = 900;
    public const int OutputSafety = 1_000;
    public const int PersistenceAndTelemetry = 1_100;
}
