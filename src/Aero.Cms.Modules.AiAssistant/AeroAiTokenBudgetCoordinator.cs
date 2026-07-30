using System.Collections.Concurrent;
using System.Globalization;
using Aero.Cms.Abstractions.Ai.Budget;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>Configuration for the provider-token admission boundary.</summary>
public sealed class AeroAiTokenBudgetOptions
{
    public const string SectionName = "AeroCms:Ai:TokenBudget";

    public bool Enabled { get; set; } = true;

    public int WindowSeconds { get; set; } = 3_600;

    public int TokenLimitPerPartition { get; set; } = 500_000;

    public int MaximumReservationTokens { get; set; } = 32_768;
}

/// <summary>
/// Strict process-local token accounting. The coordinator contract is replaceable by an atomic
/// distributed implementation for multi-instance deployments; callers always fail closed on denial.
/// </summary>
public sealed class AeroAiTokenBudgetCoordinator(
    IOptions<AeroAiTokenBudgetOptions> options,
    TimeProvider timeProvider) : IAeroAiTokenBudgetCoordinator
{
    private readonly AeroAiTokenBudgetOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, PartitionState> _partitions =
        new(StringComparer.Ordinal);

    public Task<Result<AeroAiTokenBudgetReservation>> ReserveAsync(
        AeroAiTokenBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var requestedTokensLong = request.ReservedTokens;
        if (request.EstimatedInputTokens < 0 ||
            request.MaximumOutputTokens < 0 ||
            requestedTokensLong <= 0 ||
            requestedTokensLong > _options.MaximumReservationTokens)
        {
            return Task.FromResult<Result<AeroAiTokenBudgetReservation>>(
                AeroError.ValidationError(["The requested AI token reservation is outside the configured bounds."]));
        }
        var requestedTokens = (int)requestedTokensLong;

        var now = timeProvider.GetUtcNow();
        var windowStartedOn = GetWindowStart(now);
        var reservationId = Snowflake.NewId();
        if (!_options.Enabled)
        {
            return Task.FromResult<Result<AeroAiTokenBudgetReservation>>(
                new AeroAiTokenBudgetReservation(
                    reservationId,
                    request.Scope,
                    requestedTokens,
                    windowStartedOn,
                    IsEnforced: false));
        }

        var state = _partitions.GetOrAdd(CreatePartitionKey(request.Scope), _ => new PartitionState());
        lock (state.Sync)
        {
            if (state.WindowStartedOn != windowStartedOn)
            {
                state.WindowStartedOn = windowStartedOn;
                state.ChargedTokens = 0;
                state.Reservations.Clear();
            }

            if (state.ChargedTokens + requestedTokens > _options.TokenLimitPerPartition)
            {
                return Task.FromResult<Result<AeroAiTokenBudgetReservation>>(
                    AeroError.NotAllowedError("The AI token budget is exhausted for this scope."));
            }

            state.ChargedTokens += requestedTokens;
            state.Reservations[reservationId] = requestedTokens;
        }

        return Task.FromResult<Result<AeroAiTokenBudgetReservation>>(
            new AeroAiTokenBudgetReservation(
                reservationId,
                request.Scope,
                requestedTokens,
                windowStartedOn,
                IsEnforced: true));
    }

    public Task<Result<bool>> ReconcileAsync(
        AeroAiTokenBudgetReservation reservation,
        AeroAiTokenUsage actualUsage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(actualUsage);
        cancellationToken.ThrowIfCancellationRequested();
        if (!reservation.IsEnforced)
            return Task.FromResult<Result<bool>>(true);
        if (actualUsage.InputTokens < 0 || actualUsage.OutputTokens < 0)
        {
            return Task.FromResult<Result<bool>>(
                AeroError.ValidationError(["AI token usage cannot be negative."]));
        }

        if (!_partitions.TryGetValue(CreatePartitionKey(reservation.Scope), out var state))
            return Task.FromResult<Result<bool>>(true);

        lock (state.Sync)
        {
            if (state.WindowStartedOn != reservation.WindowStartedOn ||
                !state.Reservations.Remove(reservation.ReservationId, out var reservedTokens))
            {
                return Task.FromResult<Result<bool>>(true);
            }

            state.ChargedTokens = Math.Max(
                0,
                state.ChargedTokens - reservedTokens + actualUsage.TotalTokens);
        }

        return Task.FromResult<Result<bool>>(true);
    }

    private DateTimeOffset GetWindowStart(DateTimeOffset now)
    {
        var windowTicks = TimeSpan.FromSeconds(_options.WindowSeconds).Ticks;
        var ticks = now.UtcTicks - now.UtcTicks % windowTicks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static string CreatePartitionKey(AeroAiTokenBudgetScope scope) =>
        string.Join(
            ':',
            scope.TenantId.ToString(CultureInfo.InvariantCulture),
            scope.SiteId.ToString(CultureInfo.InvariantCulture),
            ((int)scope.Audience).ToString(CultureInfo.InvariantCulture),
            scope.PrincipalId.ToString(CultureInfo.InvariantCulture),
            scope.ProviderId.Trim().ToUpperInvariant(),
            scope.Model.Trim().ToUpperInvariant());

    private sealed class PartitionState
    {
        public object Sync { get; } = new();

        public DateTimeOffset WindowStartedOn { get; set; }

        public long ChargedTokens { get; set; }

        public Dictionary<long, int> Reservations { get; } = [];
    }
}
