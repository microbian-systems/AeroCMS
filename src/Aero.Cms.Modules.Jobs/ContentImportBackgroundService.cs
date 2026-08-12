using Aero.Cms.Abstractions.Content.Importing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Jobs;

/// <summary>Opt-in scoped worker for durable, at-least-once content imports.</summary>
internal sealed class ContentImportBackgroundService(IServiceScopeFactory scopes, ILogger<ContentImportBackgroundService> logger) : BackgroundService
{
    private readonly string owner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var services = scope.ServiceProvider;
                var jobs = services.GetRequiredService<IContentImportJobStore>();
                var siteResolver = services.GetRequiredService<Aero.Cms.Core.Infrastructure.ISelectedSiteScopeResolver>();
                foreach (var source in services.GetServices<IContentImportRequestSource>())
                    foreach (var request in await source.GetRequestsAsync(stoppingToken))
                    {
                        if (!request.IsValid) { logger.LogWarning("Ignoring an invalid content import request."); continue; }
                        var selected = await siteResolver.ResolveAsync(request.SiteId, stoppingToken);
                        if (selected is { IsValid: true }) await jobs.EnsureAsync(request, selected.Value.TenantId, stoppingToken);
                    }
                var candidate = (await jobs.ListRunnableAsync(DateTimeOffset.UtcNow, 1, stoppingToken)).SingleOrDefault();
                if (candidate is not null && await jobs.TryClaimAsync(candidate.Id, owner, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), stoppingToken) is { } lease)
                {
                    var coordinator = services.GetRequiredService<IContentImportCoordinator>();
                    using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var leaseLost = 0;
                    var heartbeat = HeartbeatAsync(lease, executionCancellation, () => Interlocked.Exchange(ref leaseLost, 1), stoppingToken);
                    ContentImportProviderResult? result = null;
                    var cancelled = false;
                    var unexpectedFailure = false;
                    try
                    {
                        result = await coordinator.ExecuteAsync(lease, executionCancellation.Token);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || executionCancellation.IsCancellationRequested)
                    {
                        cancelled = true;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Unexpected content-import provider failure for job {JobId}.", lease.JobId);
                        result = ContentImportProviderResult.Failure("The content importer threw an unexpected exception.");
                        unexpectedFailure = true;
                    }
                    finally
                    {
                        executionCancellation.Cancel();
                        await heartbeat;
                    }

                    if (cancelled)
                    {
                        if (Volatile.Read(ref leaseLost) == 0 && !await jobs.ReleaseAsync(lease, CancellationToken.None))
                            logger.LogWarning("Could not release cancelled content import job {JobId}.", lease.JobId);
                        continue;
                    }
                    if (Volatile.Read(ref leaseLost) != 0)
                    {
                        logger.LogWarning("Content import job {JobId} completed after its lease was lost; its result was discarded.", lease.JobId);
                        continue;
                    }

                    var providerReportedProgress = result!.ProgressCurrent != 0 || result.ProgressTotal.HasValue;
                    var checkpoint = unexpectedFailure ? null : result.Checkpoint;
                    long? progressCurrent = !unexpectedFailure && providerReportedProgress ? result.ProgressCurrent : null;
                    long? progressTotal = !unexpectedFailure && providerReportedProgress ? result.ProgressTotal : null;
                    var finalized = result.Succeeded
                        ? await jobs.CompleteAsync(lease, stoppingToken)
                        : result.FailureDisposition == ContentImportFailureDisposition.Terminal
                            ? await jobs.FailAsync(lease, checkpoint, progressCurrent, progressTotal, result.Error ?? "The content importer failed without an error message.", stoppingToken)
                            : await jobs.RetryAsync(lease, checkpoint, progressCurrent, progressTotal, result.Error ?? "The content importer failed without an error message.", stoppingToken);
                    if (!finalized) logger.LogWarning("Content import job {JobId} finalization was rejected because its lease is no longer current.", lease.JobId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Unexpected content-import worker failure."); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task HeartbeatAsync(ContentImportLease lease, CancellationTokenSource executionCancellation, Action leaseLost, CancellationToken stoppingToken)
    {
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, executionCancellation.Token);
        try
        {
            while (!stoppingToken.IsCancellationRequested && !executionCancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), heartbeatCancellation.Token);
                await using var heartbeatScope = scopes.CreateAsyncScope();
                var store = heartbeatScope.ServiceProvider.GetRequiredService<IContentImportJobStore>();
                if (!await store.RenewAsync(lease, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), heartbeatCancellation.Token))
                {
                    logger.LogWarning("Content import job {JobId} lost its fencing lease; cancelling execution.", lease.JobId);
                    leaseLost();
                    executionCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || executionCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Content import heartbeat failed for job {JobId}; cancelling execution.", lease.JobId);
            leaseLost();
            executionCancellation.Cancel();
        }
    }
}
