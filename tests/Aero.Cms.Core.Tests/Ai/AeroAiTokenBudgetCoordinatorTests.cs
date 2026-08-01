using Aero.Cms.Abstractions.Ai.Budget;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Modules.AiAssistant;
using Aero.Core.Railway;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroAiTokenBudgetCoordinatorTests
{
    [Test]
    public async Task Concurrent_reservations_never_exceed_the_partition_token_limit()
    {
        var coordinator = CreateCoordinator(tokenLimit: 100, maximumReservation: 20);
        var tasks = Enumerable.Range(0, 20)
            .Select(index => coordinator.ReserveAsync(Request(Scope(), 10, $"request-{index}")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(result => result is Result<AeroAiTokenBudgetReservation>.Ok)
            .ShouldBe(10);
        results.Count(result => result is Result<AeroAiTokenBudgetReservation>.Failure)
            .ShouldBe(10);
    }

    [Test]
    public async Task Reconciliation_refunds_unused_tokens_and_partitions_remain_isolated()
    {
        var coordinator = CreateCoordinator(tokenLimit: 100, maximumReservation: 100);
        var first = await coordinator.ReserveAsync(Request(Scope(), 80, "first"));
        var denied = await coordinator.ReserveAsync(Request(Scope(), 30, "denied"));
        var otherSite = await coordinator.ReserveAsync(
            Request(Scope() with { SiteId = 74 }, 100, "other-site"));

        var reservation = first
            .ShouldBeOfType<Result<AeroAiTokenBudgetReservation>.Ok>()
            .Value;
        var reconciled = await coordinator.ReconcileAsync(
            reservation,
            new AeroAiTokenUsage(10, 10));
        var admittedAfterRefund = await coordinator.ReserveAsync(
            Request(Scope(), 30, "after-refund"));

        denied.ShouldBeOfType<Result<AeroAiTokenBudgetReservation>.Failure>();
        otherSite.ShouldBeOfType<Result<AeroAiTokenBudgetReservation>.Ok>();
        reconciled.ShouldBeOfType<Result<bool>.Ok>();
        admittedAfterRefund.ShouldBeOfType<Result<AeroAiTokenBudgetReservation>.Ok>();
    }

    [Test]
    public async Task Reconciliation_is_idempotent_and_actual_overage_blocks_later_work()
    {
        var coordinator = CreateCoordinator(tokenLimit: 100, maximumReservation: 100);
        var reservation = (await coordinator.ReserveAsync(Request(Scope(), 60, "first")))
            .ShouldBeOfType<Result<AeroAiTokenBudgetReservation>.Ok>()
            .Value;

        var firstReconciliation = await coordinator.ReconcileAsync(
            reservation,
            new AeroAiTokenUsage(70, 20));
        var duplicateReconciliation = await coordinator.ReconcileAsync(
            reservation,
            new AeroAiTokenUsage(0, 0));
        var denied = await coordinator.ReserveAsync(Request(Scope(), 20, "later"));

        firstReconciliation.ShouldBeOfType<Result<bool>.Ok>();
        duplicateReconciliation.ShouldBeOfType<Result<bool>.Ok>();
        denied.ShouldBeOfType<Result<AeroAiTokenBudgetReservation>.Failure>();
    }

    private static AeroAiTokenBudgetCoordinator CreateCoordinator(
        int tokenLimit,
        int maximumReservation)
        => new(
            Options.Create(new AeroAiTokenBudgetOptions
            {
                Enabled = true,
                WindowSeconds = 3_600,
                TokenLimitPerPartition = tokenLimit,
                MaximumReservationTokens = maximumReservation
            }),
            TimeProvider.System);

    private static AeroAiTokenBudgetRequest Request(
        AeroAiTokenBudgetScope scope,
        int tokens,
        string correlationId)
        => new(scope, EstimatedInputTokens: tokens - 1, MaximumOutputTokens: 1, correlationId);

    private static AeroAiTokenBudgetScope Scope()
        => new(
            TenantId: 41,
            SiteId: 73,
            AeroAiAudience.Manager,
            PrincipalId: 97,
            ProviderId: "provider",
            Model: "model");
}
