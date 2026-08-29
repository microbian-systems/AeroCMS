using Aero.Cms.Abstractions.Content.Importing;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Modules.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content.Importing;

public sealed class ContentImportBackgroundServiceTests
{
    [Test]
    public async Task Polling_and_heartbeat_scopes_asynchronously_dispose_async_only_scoped_dependencies()
    {
        var tracker = new ScopeDisposalTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddScoped<IContentImportJobStore, AsyncOnlyJobStore>();
        services.AddSingleton<IContentImportCoordinator, WaitingCoordinator>();
        services.AddSingleton<ISelectedSiteScopeResolver, UnusedSiteScopeResolver>();
        await using var provider = services.BuildServiceProvider();
        var service = new ContentImportBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ContentImportBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await tracker.HeartbeatRenewed.Task.WaitAsync(TimeSpan.FromSeconds(35));
            await tracker.ScopesDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            tracker.DisposedScopes.ShouldBe(2);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class ScopeDisposalTracker
    {
        public TaskCompletionSource HeartbeatRenewed { get; } = NewSignal();
        public TaskCompletionSource ScopesDisposed { get; } = NewSignal();
        public int DisposedScopes { get; private set; }

        public void DisposeScope()
        {
            DisposedScopes++;
            if (DisposedScopes == 2) ScopesDisposed.TrySetResult();
        }

        private static TaskCompletionSource NewSignal()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class AsyncOnlyJobStore(ScopeDisposalTracker tracker) : IContentImportJobStore, IAsyncDisposable
    {
        private static readonly ContentImportLease Lease = new(1, "lease", 1, DateTimeOffset.UtcNow.AddMinutes(2));

        public Task<ContentImportJob?> LoadAsync(long jobId, CancellationToken ct = default) => Task.FromResult<ContentImportJob?>(null);
        public Task<ContentImportJob?> EnsureAsync(ContentImportRequest request, long tenantId, CancellationToken ct = default) => Task.FromResult<ContentImportJob?>(null);
        public Task<ContentImportLease?> TryClaimAsync(long jobId, string owner, DateTimeOffset now, TimeSpan duration, CancellationToken ct = default) => Task.FromResult<ContentImportLease?>(Lease);
        public Task<bool> RenewAsync(ContentImportLease lease, DateTimeOffset now, TimeSpan duration, CancellationToken ct = default)
        {
            tracker.HeartbeatRenewed.TrySetResult();
            return Task.FromResult(true);
        }
        public Task<bool> ReportAsync(ContentImportLease lease, string? checkpoint, long progressCurrent, long? progressTotal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CompleteAsync(ContentImportLease lease, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RetryAsync(ContentImportLease lease, string? checkpoint, long? progressCurrent, long? progressTotal, string error, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> FailAsync(ContentImportLease lease, string? checkpoint, long? progressCurrent, long? progressTotal, string error, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ReleaseAsync(ContentImportLease lease, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<ContentImportJob>> ListRunnableAsync(DateTimeOffset now, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ContentImportJob>>([new ContentImportJob(Lease.JobId, "request", 1, Request(), ContentImportJobState.Pending, 0, null, 0, null, null, null, 0, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]);

        public ValueTask DisposeAsync()
        {
            tracker.DisposeScope();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class WaitingCoordinator(ScopeDisposalTracker tracker) : IContentImportCoordinator
    {
        public async Task<ContentImportProviderResult> ExecuteAsync(ContentImportLease lease, CancellationToken ct = default)
        {
            await tracker.HeartbeatRenewed.Task.WaitAsync(ct);
            return ContentImportProviderResult.Success();
        }
    }

    private sealed class UnusedSiteScopeResolver : ISelectedSiteScopeResolver
    {
        public Task<SelectedSiteScope?> ResolveAsync(long selectedSiteId, CancellationToken cancellationToken = default)
            => Task.FromResult<SelectedSiteScope?>(null);
    }

    private static ContentImportRequest Request() => new(1, "test", "1", "source", "selection", "{}", "test", false);
}
