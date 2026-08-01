using System.Globalization;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Basket.Api;

/// <summary>Maps external-member basket endpoints using the host-resolved storefront scope.</summary>
public static class BasketEndpoints
{
    public static IEndpointRouteBuilder MapBasketApi(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/commerce/basket").RequireAuthorization(ExternalMemberAuthenticationDefaults.Policy, ExternalMemberAuthenticationDefaults.SitePolicy);
        group.MapGet("/", async (IBasketService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) => await Current(service.GetOrCreateAsync(site.TenantId, site.SiteId, Member(principal), ct)));
        group.MapPost("/items", async (AddBasketItemRequest request, IBasketService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) => await Current(service.AddItemAsync(site.TenantId, site.SiteId, Member(principal), request.ListingId, request.Quantity, CultureInfo.CurrentUICulture.Name, ct)));
        group.MapPut("/items/{listingId:long}", async (long listingId, UpdateBasketQuantityRequest request, IBasketService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) => await Current(service.UpdateQuantityAsync(site.TenantId, site.SiteId, Member(principal), listingId, request.Quantity, CultureInfo.CurrentUICulture.Name, ct)));
        group.MapDelete("/items/{listingId:long}", async (long listingId, IBasketService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) => await Current(service.UpdateQuantityAsync(site.TenantId, site.SiteId, Member(principal), listingId, 0, CultureInfo.CurrentUICulture.Name, ct)));
        group.MapDelete("/", async (IBasketService service, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) => await Current(service.ClearAsync(site.TenantId, site.SiteId, Member(principal), ct)));
        return builder;
    }
    private static long Member(ICurrentPrincipal principal) => principal.PrincipalId ?? throw new BadHttpRequestException("External member is required.");
    private static async Task<IResult> Current(Task<Result<Models.BasketDocument, AeroError>> operation) => await operation is Result<Models.BasketDocument, AeroError>.Ok(var basket) ? Results.Ok(basket) : Results.BadRequest();
}

/// <summary>Basket input intentionally contains no customer, price, name, SKU, or product snapshot.</summary>
public sealed record AddBasketItemRequest(long ListingId, int Quantity);
/// <summary>Updates only an existing line quantity.</summary>
public sealed record UpdateBasketQuantityRequest(int Quantity);
