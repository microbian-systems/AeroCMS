using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.SiteMap;

public class SitemapInvalidationHandler(IFusionCache cache) : IWolverineHandler
{
    public Task Handle(AeroEvent<PageViewModel>.PageCreated _) => Invalidate();
    public Task Handle(AeroEvent<PageViewModel>.PageUpdated _) => Invalidate();
    public Task Handle(AeroEvent<PageViewModel>.PageDeleted _) => Invalidate();
    public Task Handle(AeroEvent<PostViewModel>.PostCreated _) => Invalidate();
    public Task Handle(AeroEvent<PostViewModel>.PostUpdated _) => Invalidate();
    public Task Handle(AeroEvent<PostViewModel>.PostDeleted _) => Invalidate();
    public Task Handle(AeroEvent<DocViewModel>.DocCreated _) => Invalidate();
    public Task Handle(AeroEvent<DocViewModel>.DocUpdated _) => Invalidate();
    public Task Handle(AeroEvent<DocViewModel>.DocDeleted _) => Invalidate();

    private Task Invalidate() => cache.RemoveAsync("sitemap:xml").AsTask();
}
