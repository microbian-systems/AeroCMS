using Aero.Cms.Abstractions.Content.Views;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>Evicts anonymous page responses tagged as consuming a virtual content view.</summary>
internal sealed class ContentViewOutputCacheInvalidator(IOutputCacheStore outputCache) : IContentViewOutputCacheInvalidator
{
    public async Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return;
        await outputCache.EvictByTagAsync(ContentViewOutputCacheTags.Site(scope), ct);
    }
}
