using Aero.Cms.Abstractions.Content.Importing;
using Aero.Cms.Modules.Jobs;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content.Importing;

public sealed class SableContentImportJobStoreTests
{
    [Test]
    public async Task Ensure_uses_canonical_request_identity_and_runnable_queries_are_bounded()
    {
        await using var harness = await CreateHarnessAsync();
        var store = new SableContentImportJobStore(harness.Store);

        var first = await store.EnsureAsync(Request("{\"source\":\"col\",\"take\":20}"), 3);
        var replay = await store.EnsureAsync(Request("{\"take\":20,\"source\":\"col\"}"), 3);
        var second = await store.EnsureAsync(Request("{\"take\":21,\"source\":\"col\"}"), 3);
        var third = await store.EnsureAsync(Request("{\"take\":22,\"source\":\"col\"}"), 3);

        first.ShouldNotBeNull();
        replay.ShouldNotBeNull();
        second.ShouldNotBeNull();
        third.ShouldNotBeNull();
        replay.Id.ShouldBe(first.Id);
        (await store.ListRunnableAsync(DateTimeOffset.UtcNow, 2)).Count.ShouldBe(2);
        (await store.ListRunnableAsync(DateTimeOffset.UtcNow, 101)).ShouldBeEmpty();
    }

    [Test]
    public async Task Fencing_rejects_stale_mutation_and_progress_never_moves_backwards()
    {
        await using var harness = await CreateHarnessAsync();
        var store = new SableContentImportJobStore(harness.Store);
        var job = (await store.EnsureAsync(Request("{\"take\":20}"), 3))!;
        var now = DateTimeOffset.UtcNow;
        var first = (await store.TryClaimAsync(job.Id, "worker-a", now, TimeSpan.FromMinutes(1)))!;
        var second = (await store.TryClaimAsync(job.Id, "worker-b", now.AddMinutes(2), TimeSpan.FromMinutes(1)))!;

        (await store.ReportAsync(second, "row-10", 10, 20)).ShouldBeTrue();
        (await store.ReportAsync(second, "row-9", 9, 20)).ShouldBeFalse();
        (await store.CompleteAsync(first)).ShouldBeFalse();

        var persisted = (await store.LoadAsync(job.Id))!;
        persisted.ProgressCurrent.ShouldBe(10);
        persisted.Checkpoint.ShouldBe("row-10");
        persisted.State.ShouldBe(ContentImportJobState.Running);
    }

    [Test]
    public async Task Retry_preserves_durable_progress_and_attempt_cap_requires_manual_review()
    {
        await using var harness = await CreateHarnessAsync();
        var store = new SableContentImportJobStore(harness.Store);
        var job = (await store.EnsureAsync(Request("{\"take\":20}"), 3))!;
        var now = DateTimeOffset.UtcNow;
        var first = (await store.TryClaimAsync(job.Id, "worker", now, TimeSpan.FromMinutes(1)))!;

        (await store.ReportAsync(first, "row-8", 8, 20)).ShouldBeTrue();
        (await store.RetryAsync(first, "row-9-cursor", null, null, "transient source error")).ShouldBeTrue();
        var retried = (await store.LoadAsync(job.Id))!;
        retried.State.ShouldBe(ContentImportJobState.Pending);
        retried.ProgressCurrent.ShouldBe(8);
        retried.Checkpoint.ShouldBe("row-9-cursor");
        retried.LastError.ShouldBe("transient source error");
        retried.NextAttemptOn.ShouldNotBeNull();
        (await store.TryClaimAsync(
            job.Id,
            "worker-too-early",
            retried.NextAttemptOn.Value.AddTicks(-1),
            TimeSpan.FromMinutes(1))).ShouldBeNull();

        // Expired leases are recoverable, but the eighth claim is the final automatic attempt.
        var clock = now.AddHours(2);
        for (var attempt = 2; attempt <= 8; attempt++)
        {
            var lease = await store.TryClaimAsync(job.Id, $"worker-{attempt}", clock, TimeSpan.FromMinutes(1));
            lease.ShouldNotBeNull();
            clock = clock.AddMinutes(2);
        }

        (await store.TryClaimAsync(job.Id, "worker-9", clock, TimeSpan.FromMinutes(1))).ShouldBeNull();
        var capped = (await store.LoadAsync(job.Id))!;
        capped.State.ShouldBe(ContentImportJobState.ManualReview);
        capped.Attempt.ShouldBe(8);
        capped.LeaseToken.ShouldBeNull();
        capped.NextAttemptOn.ShouldBeNull();
    }

    [Test]
    public async Task Terminal_failure_persists_provider_checkpoint_and_progress()
    {
        await using var harness = await CreateHarnessAsync();
        var store = new SableContentImportJobStore(harness.Store);
        var job = (await store.EnsureAsync(Request("{\"take\":20}"), 3))!;
        var lease = (await store.TryClaimAsync(job.Id, "worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)))!;

        (await store.FailAsync(lease, "malformed-row", 4, 20, "invalid source row")).ShouldBeTrue();

        var failed = (await store.LoadAsync(job.Id))!;
        failed.State.ShouldBe(ContentImportJobState.Failed);
        failed.Checkpoint.ShouldBe("malformed-row");
        failed.ProgressCurrent.ShouldBe(4);
        failed.ProgressTotal.ShouldBe(20);
        failed.LastError.ShouldBe("invalid source row");
        failed.LeaseToken.ShouldBeNull();
    }

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(options => new JobsModule().Configure(options));
        await harness.InitializeAsync();
        return harness;
    }

    private static ContentImportRequest Request(string optionsJson) => new(
        7, "test-importer", "v1", "source-sha", "selection", optionsJson, "system:test", false);
}
