using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Infrastructure;
using System.Globalization;

namespace Aero.Cms.Modules.Sites;

/// <summary>Adapts the site's host lookup to the generic public endpoint-selection contract.</summary>
public sealed class PublicSiteRouteResolver(ISiteLookupService sites) : IPublicSiteRouteResolver
{
    /// <inheritdoc />
    public async Task<PublicSiteRouteScope?> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        var site = await sites.ResolveByHostAsync(HostNormalizer.Normalize(host), cancellationToken);
        if (site is null || !site.IsEnabled)
            return null;

        var defaultCulture = NormalizeCulture(site.DefaultCulture);
        if (defaultCulture is null)
            return null;
        var cultures = site.SupportedCultures
            .Select(NormalizeCulture)
            .Where(culture => culture is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!cultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
            cultures.Insert(0, defaultCulture);
        return new PublicSiteRouteScope(site.Id, defaultCulture, cultures);
    }

    private static string? NormalizeCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try { return CultureInfo.GetCultureInfo(value.Trim()).Name; }
        catch (CultureNotFoundException) { return null; }
    }
}
