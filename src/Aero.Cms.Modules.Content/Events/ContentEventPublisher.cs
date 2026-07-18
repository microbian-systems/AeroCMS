using Wolverine;

namespace Aero.Cms.Modules.Content.Events;

/// <summary>
/// Publishes non-durable content notifications after persistence commits.
/// Notification delivery is best effort and cannot turn a committed write
/// into an apparent request failure. Commerce-grade durability is a separate
/// outbox concern.
/// </summary>
internal sealed class ContentEventPublisher(
    IMessageBus messageBus,
    ILogger<ContentEventPublisher> logger)
{
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
