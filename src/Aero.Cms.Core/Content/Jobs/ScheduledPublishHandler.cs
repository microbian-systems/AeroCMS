using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Services;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Jobs;

/// <summary>
/// Evaluates content items scheduled for publish or unpublish.
/// Triggered by a recurring job (Wolverine or TickerQ).
/// Updates each due item's PublicationState and clears the schedule field.
/// </summary>
/// <remarks>
/// The handler provides no concurrency lock, lease, or idempotency token. Each item is mutated
/// and committed separately through <see cref="IContentService"/>. It does not run content
/// validation, increment version numbers, create version snapshots, or coordinate all due items
/// in one transaction.
/// </remarks>
public sealed class ScheduledPublishHandler(
    IDocumentSession session,
    IContentService contentService,
    ContentCommandService commands)
{
    /// <summary>
    /// Processes all content items that are due for scheduled publish or unpublish.
    /// </summary>
    /// <param name="ct">A token that can cancel queries or per-item saves.</param>
    /// <remarks>
    /// Publish and unpublish candidates are queried independently using the current UTC time.
    /// Publish candidates receive a new publication timestamp; unpublish candidates retain
    /// their existing timestamp. Returned <see cref="Aero.Core.Railway.Result{T, TError}"/>
    /// values from per-item saves are not inspected, so a failed result does not stop later
    /// processing. Exceptions and cancellation do stop the handler and propagate.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    public async Task Handle(CancellationToken ct = default)
    {
        // Find items due for publish
        var dueItems = await session.Query<ContentItem>()
            .Where(i => i.SchedulePublishUtc <= DateTimeOffset.UtcNow
                     && i.PublicationState == ContentPublicationState.Draft)
            .ToListAsync(ct);

        foreach (var item in dueItems)
        {
            // Scheduled publication uses the identical validation and AI-review gate as an
            // interactive publish. A rejected result leaves the schedule intact for editorial
            // correction instead of silently publishing an unreviewed translation.
            var scheduledAt = item.SchedulePublishUtc;
            item.SchedulePublishUtc = null;
            var result = await commands.PublishAsync(item, ct);
            if (result is not Aero.Core.Railway.Result<ContentItem, Aero.Core.AeroError>.Ok)
            {
                item.SchedulePublishUtc = scheduledAt;
            }
        }

        // Find items due for unpublish
        var dueUnpublish = await session.Query<ContentItem>()
            .Where(i => i.ScheduleUnpublishUtc <= DateTimeOffset.UtcNow
                     && i.PublicationState == ContentPublicationState.Published)
            .ToListAsync(ct);

        foreach (var item in dueUnpublish)
        {
            item.PublicationState = ContentPublicationState.Draft;
            item.ScheduleUnpublishUtc = null;
            await contentService.SaveAsync(item, ct);
        }
    }
}
