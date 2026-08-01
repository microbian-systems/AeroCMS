using Wolverine;

namespace Aero.Cms.Modules.Content.Events;

/// <summary>
/// Publishes non-durable content notifications after persistence commits.
/// Notification delivery is best effort and cannot turn a committed write
/// into an apparent request failure. Commerce-grade durability is a separate
/// outbox concern.
/// </summary>
/// <param name="messageBus">The Wolverine bus used for in-process or transport publication.</param>
/// <param name="logger">The logger for suppressed publication failures.</param>
internal sealed class ContentEventPublisher(
    IMessageBus messageBus,
    ILogger<ContentEventPublisher> logger)
{
    /// <summary>
    /// Publishes a notification and logs any exception without failing the committed operation.
    /// </summary>
    /// <typeparam name="T">The non-null message contract.</typeparam>
    /// <param name="message">The notification to publish.</param>
    public async Task PublishBestEffortAsync<T>(T message)
        where T : notnull
    {
        try
        {
            await messageBus.PublishAsync(message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Content was persisted, but notification {MessageType} was not published.",
                typeof(T).Name);
        }
    }
}
