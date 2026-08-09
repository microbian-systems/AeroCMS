using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.Transports.Local;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>
/// Refreshes disposable AI knowledge projections after committed CMS mutations.
/// </summary>
/// <remarks>
/// The originating aggregate commit and this projection intentionally use separate units of work.
/// The named sequential queue serializes each scoped query, reconciliation, and save unit so duplicate
/// Save and Publish lifecycle events can replay safely against stable chunk slots.
/// </remarks>
[WolverineHandler]
[StickyHandler("aero-ai-knowledge-projections")]
public sealed class AeroAiKnowledgeProjectionHandler(
    IDocumentSession session,
    IAeroAiKnowledgeProjectionService projectionService)
    : IConfigureLocalQueue
{
    public static void Configure(LocalQueueConfiguration configuration)
    {
        configuration.Sequential();
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
