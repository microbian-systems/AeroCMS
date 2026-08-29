using Aero.Cms.Modules.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentTranslationProjectionWorkHostedServiceTests
{
    [Test]
    public async Task Polling_scope_asynchronously_disposes_async_only_processor()
    {
        var tracker = new DisposalTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddScoped<IContentTranslationProjectionWorkProcessor, AsyncOnlyProcessor>();
        await using var provider = services.BuildServiceProvider();
        var service = new ContentTranslationProjectionWorkHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ContentTranslationProjectionWorkHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await tracker.Processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await tracker.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            tracker.DisposeCount.ShouldBe(1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class DisposalTracker
    {
        public TaskCompletionSource Processed { get; } = NewSignal();
        public TaskCompletionSource Disposed { get; } = NewSignal();
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            Disposed.TrySetResult();
        }

        private static TaskCompletionSource NewSignal()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class AsyncOnlyProcessor(DisposalTracker tracker) : IContentTranslationProjectionWorkProcessor, IAsyncDisposable
    {
        public Task<bool> ProcessNextBatchAsync(int take, CancellationToken cancellationToken = default)
        {
            tracker.Processed.TrySetResult();
            return Task.FromResult(false);
        }

        public ValueTask DisposeAsync()
        {
            tracker.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
