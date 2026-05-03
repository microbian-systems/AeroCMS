using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Services;
using Marten;

namespace Aero.Cms.Core.Content.Jobs;

/// <summary>
/// Evaluates content items scheduled for publish or unpublish.
/// Triggered by a recurring job (Wolverine or TickerQ).
/// Updates each due item's PublicationState and clears the schedule field.
/// </summary>
public sealed class ScheduledPublishHandler(
    IDocumentSession session,
    IContentService contentService)
{
    /// <summary>
    /// Processes all content items that are due for scheduled publish or unpublish.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(CancellationToken ct = default)
    {
        // Find items due for publish
        var dueItems = await session.Query<ContentItem>()
            .Where(i => i.SchedulePublishUtc <= DateTimeOffset.UtcNow
                     && i.PublicationState == ContentPublicationState.Draft)
            .ToListAsync(ct);

        foreach (var item in dueItems)
        {
            item.PublicationState = ContentPublicationState.Published;
            item.PublishedOn = DateTimeOffset.UtcNow;
            item.SchedulePublishUtc = null;
            await contentService.SaveAsync(item, ct);
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
