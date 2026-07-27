using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Ai.Budget;

/// <summary>
/// Identifies one provider-usage partition. Every value is supplied by trusted server context.
/// </summary>
public sealed record AeroAiTokenBudgetScope(
    long TenantId,
    long SiteId,
    AeroAiAudience Audience,
    long PrincipalId,
    string ProviderId,
    string Model);

/// <summary>Requests a conservative token reservation before a provider invocation begins.</summary>
public sealed record AeroAiTokenBudgetRequest(
    AeroAiTokenBudgetScope Scope,
    int EstimatedInputTokens,
    int MaximumOutputTokens,
    string CorrelationId)
{
    /// <summary>Gets the maximum number of tokens that the invocation may consume.</summary>
    public long ReservedTokens => (long)EstimatedInputTokens + MaximumOutputTokens;
}

/// <summary>
/// Represents tokens charged to one budget window before provider work starts.
/// </summary>
public sealed record AeroAiTokenBudgetReservation(
    long ReservationId,
    AeroAiTokenBudgetScope Scope,
    int ReservedTokens,
    DateTimeOffset WindowStartedOn,
    bool IsEnforced);

/// <summary>Provider-reported or conservatively estimated usage for one invocation.</summary>
public sealed record AeroAiTokenUsage(int InputTokens, int OutputTokens)
{
    public long TotalTokens => (long)InputTokens + OutputTokens;
}

/// <summary>
/// Reserves and reconciles provider tokens. Deployments may replace this contract with an
/// atomic distributed implementation without changing callers.
/// </summary>
public interface IAeroAiTokenBudgetCoordinator
{
    Task<Result<AeroAiTokenBudgetReservation>> ReserveAsync(
        AeroAiTokenBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ReconcileAsync(
        AeroAiTokenBudgetReservation reservation,
        AeroAiTokenUsage actualUsage,
        CancellationToken cancellationToken = default);
}
