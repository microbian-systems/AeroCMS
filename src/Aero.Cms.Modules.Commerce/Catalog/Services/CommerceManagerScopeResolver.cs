using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

/// <summary>Represents the persisted site and tenant boundary for one manager catalog request.</summary>
public sealed record CommerceManagerScope(long TenantId, long SiteId);

/// <summary>Resolves manager catalog ownership from the selected persisted site.</summary>
public interface ICommerceManagerScopeResolver
{
    Task<Result<CommerceManagerScope, AeroError>> ResolveAsync(CancellationToken ct = default);
}

/// <summary>
/// Treats <see cref="ISiteContext.SiteId"/> as an unverified manager selection and derives the
/// tenant identifier only from the matching persisted site record.
/// </summary>
public sealed class CommerceManagerScopeResolver(
    ISiteContext siteContext,
    IHttpContextAccessor httpContextAccessor,
    IDocumentSession session)
    : ICommerceManagerScopeResolver
{
    public async Task<Result<CommerceManagerScope, AeroError>> ResolveAsync(CancellationToken ct = default)
    {
        var cookie = httpContextAccessor.HttpContext?.Request.Cookies["AeroCms.SiteId"];
        if (!long.TryParse(cookie, out var selectedSiteId) ||
            selectedSiteId <= 0 ||
            siteContext.SiteId != selectedSiteId)
            return Prelude.Fail<CommerceManagerScope, AeroError>(AeroError.NotFoundError("Site not found."));

        try
        {
            var site = await session.Query<SitesModel>()
                .FirstOrDefaultAsync(x => x.Id == selectedSiteId, ct);
            return site is { TenantId: > 0 }
                ? Prelude.Ok<CommerceManagerScope, AeroError>(new CommerceManagerScope(site.TenantId, site.Id))
                : Prelude.Fail<CommerceManagerScope, AeroError>(AeroError.NotFoundError("Site not found."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Prelude.Fail<CommerceManagerScope, AeroError>(AeroError.DatabaseError("Site scope could not be resolved."));
        }
    }
}
