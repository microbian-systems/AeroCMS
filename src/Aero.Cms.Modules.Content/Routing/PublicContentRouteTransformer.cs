using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Shared.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Content.Routing;

/// <summary>
/// Selects the public content page only for an enabled host, configured route culture, and an
/// existing content type that explicitly permits public URLs.
/// </summary>
/// <remarks>
/// This dynamic selector runs before site-resolution middleware. It therefore resolves the
/// host through <see cref="IPublicSiteRouteResolver"/> rather than accepting a site identifier
/// from the request. Returning <see langword="null"/> leaves ordinary Pages, Posts, and Docs
/// endpoints eligible for selection.
/// </remarks>
public sealed class PublicContentRouteTransformer(
    IPublicSiteRouteResolver sites,
    IContentTypeService contentTypes) : DynamicRouteValueTransformer
{
    /// <inheritdoc />
    public override async ValueTask<RouteValueDictionary> TransformAsync(
        HttpContext httpContext,
        RouteValueDictionary values)
    {
        var cultureAlias = values["culture"]?.ToString();
        var typeAlias = values["typeAlias"]?.ToString()?.Trim().Trim('/');
        var entrySlug = values["entrySlug"]?.ToString()?.Trim().Trim('/').TrimEnd('.');
        if (string.IsNullOrWhiteSpace(cultureAlias) ||
            string.IsNullOrWhiteSpace(typeAlias) ||
            string.IsNullOrWhiteSpace(entrySlug))
        {
            return null!;
        }

        var site = await sites.ResolveAsync(
            HostNormalizer.Normalize(httpContext.Request.Host.Host),
            httpContext.RequestAborted);
        if (site is null ||
            !AeroCultureRoute.TryResolveSupportedCultureAlias(cultureAlias, site.SupportedCultures, out var culture))
        {
            return null!;
        }

        var type = await contentTypes.GetByAliasAsync(site.SiteId, typeAlias, httpContext.RequestAborted);
        if (type is not Aero.Core.Railway.Result<Aero.Cms.Abstractions.Content.ContentTypeDefinition, Aero.Core.AeroError>.Ok ok ||
            !ok.Value.AllowPublicUrl)
        {
            return null!;
        }

        return new RouteValueDictionary
        {
            ["area"] = "Content",
            ["page"] = "/PublicContent",
            ["culture"] = culture,
            ["typeAlias"] = typeAlias,
            ["entrySlug"] = entrySlug
        };
    }
}

/// <summary>Fails closed when a host does not install the site-resolution module.</summary>
public sealed class DisabledPublicSiteRouteResolver : IPublicSiteRouteResolver
{
    /// <inheritdoc />
    public Task<PublicSiteRouteScope?> ResolveAsync(string host, CancellationToken cancellationToken = default)
        => Task.FromResult<PublicSiteRouteScope?>(null);
}
