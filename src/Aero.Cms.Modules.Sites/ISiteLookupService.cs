using Aero.Cms.Abstractions.Models;
using Aero.Core;
using Marten;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Sites;

public interface ISiteLookupService
{
    Task<IReadOnlyList<SiteViewModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SiteViewModel?> ResolveByHostAsync(string host, CancellationToken cancellationToken = default);
}

public interface IAeroSiteSlice
{
    long SiteId { get; }
    long TenantId { get; }
}

public class AeroSiteSlice : IAeroSiteSlice
{
    public long SiteId { get; init; }
    public long TenantId { get; init; }
}

public class AeroSiteMiddleware(RequestDelegate next)
{

    public interface IAeroSiteResolver
    {
        Task<SiteViewModel?> ResolveByHostAsync(string host);
    }

    // Simple implementation using Marten
    public class MartenSiteResolver : IAeroSiteResolver
    {
        private readonly IDocumentSession _session;
        public MartenSiteResolver(IDocumentSession session) => _session = session;

        public async Task<SiteViewModel?> ResolveByHostAsync(string host)
        {
            // In production, wrap this in a memory cache!
            return await _session.Query<SiteViewModel>()
                .FirstOrDefaultAsync(x => x.Hosts.Contains(host));
        }
    }

    public async Task InvokeAsync(HttpContext context, IAeroSiteResolver resolver)
    {
        // 1. Get the hostname (e.g., "mysite.com" or "tenant1.aero.io")
        var host = context.Request.Host.Host;

        // 2. Resolve the site from your DB/Cache
        var site = await resolver.ResolveByHostAsync(host);

        if (site == null)
        {
            // Handle unknown sites (404 or a default landing page)
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Uknown host");
            return;
        }

        // 3. Attach the site info to the request features
        context.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            SiteId = site.Id,
            TenantId = Snowflake.NewId() // todo - get this from middleware (previous in chain)
        });

        await next(context);
    }
}