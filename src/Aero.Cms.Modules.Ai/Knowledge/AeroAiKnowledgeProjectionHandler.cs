using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;
using Wolverine.Transports.Local;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Refreshes disposable AI knowledge projections after committed CMS mutations.
/// </summary>
/// <remarks>
/// The originating aggregate commit and this projection intentionally use separate units of work.
/// Projection changes reconcile stable chunk slots, so Wolverine can safely reschedule the complete
/// handler in a fresh scope and document session when SurrealDB reports a transaction conflict.
/// A failed <c>SaveChangesAsync</c> or <c>COMMIT</c> is never retried in place.
/// </remarks>
[WolverineHandler]
[StickyHandler("aero-ai-knowledge-projections")]
public sealed class AeroAiKnowledgeProjectionHandler(
    IDocumentSession session,
    IAeroAiKnowledgeProjectionService projectionService)
    : IConfigureLocalQueue, IHandlerConfiguration
{
    public static void Configure(LocalQueueConfiguration configuration)
    {
        configuration.Sequential();
    }

    public static void Configure(HandlerChain chain)
    {
        chain.OnException<TransactionConflictException>()
            .ScheduleRetry(
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(150),
                TimeSpan.FromMilliseconds(500))
            .WithBoundedJitter(0.2);
    }

    public Task Handle(
        PageContentUpdatedEvent message,
        CancellationToken cancellationToken)
        => ProjectOrDeleteAsync<PageDocument>(
            message.ContentId,
            message.SiteId,
            AeroAiKnowledgeSourceKinds.Page,
            AeroAiCmsKnowledgeSourceFactory.Create,
            cancellationToken);

    public Task Handle(
        BlogPostContentUpdatedEvent message,
        CancellationToken cancellationToken)
        => ProjectOrDeleteAsync<PostDocument>(
            message.ContentId,
            message.SiteId,
            AeroAiKnowledgeSourceKinds.Post,
            AeroAiCmsKnowledgeSourceFactory.Create,
            cancellationToken);

    public Task Handle(
        DocsPageContentUpdatedEvent message,
        CancellationToken cancellationToken)
        => ProjectOrDeleteAsync<DocsPage>(
            message.ContentId,
            message.SiteId,
            AeroAiKnowledgeSourceKinds.Docs,
            AeroAiCmsKnowledgeSourceFactory.Create,
            cancellationToken);

    private async Task ProjectOrDeleteAsync<TDocument>(
        long sourceId,
        long siteId,
        string sourceKind,
        Func<TDocument, AeroAiKnowledgeSource> createSource,
        CancellationToken cancellationToken)
        where TDocument : SableDocument
    {
        var document = await session.Query<TDocument>()
            .FirstOrDefaultAsync(
                value => value.Id == sourceId,
                cancellationToken);

        if (document is ISiteOwned siteOwned && siteOwned.SiteId == siteId)
        {
            await projectionService.StageUpsertAsync(
                createSource(document),
                cancellationToken);
        }
        else
        {
            await projectionService.StageDeleteAsync(
                tenantId: 0,
                siteId,
                sourceKind,
                sourceId,
                cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
