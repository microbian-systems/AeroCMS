using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Content;

/// <summary>Drains bounded localization projection repair batches in independent scopes.</summary>
internal sealed class ContentTranslationProjectionWorkHostedService(
    IServiceScopeFactory scopes,
    ILogger<ContentTranslationProjectionWorkHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IContentTranslationProjectionWorkProcessor>();
                if (await processor.ProcessNextBatchAsync(100, stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Localized content projection work will be retried.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
