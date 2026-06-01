using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Shared.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Aero.Cms.Web.Bootstrap.Localization;

public sealed class AeroRequestCultureProvider : RequestCultureProvider
{
    private static readonly PathString ManagerPathPrefix = "/manager";

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var site = httpContext.Features.Get<IAeroSiteSlice>();
        var defaultCulture = site?.DefaultCulture ?? Options?.DefaultRequestCulture.Culture.Name;
        var supportedCultures = site?.SupportedCultures is { Count: > 0 }
            ? site.SupportedCultures
            : Options?.SupportedUICultures?.Select(culture => culture.Name);

        var selectedCulture = AeroCultureRoute.ResolveRequestCulture(
            httpContext.Request.Path,
            defaultCulture,
            supportedCultures,
            out var pathCulture);

        if (pathCulture is not null)
        {
            SetCultureItems(httpContext, selectedCulture, pathCulture);
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(selectedCulture, selectedCulture));
        }

        if (site is not null)
        {
            SetCultureItems(httpContext, selectedCulture, pathCulture);
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(selectedCulture, selectedCulture));
        }

        if (httpContext.Request.Path.StartsWithSegments(ManagerPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        return Task.FromResult<ProviderCultureResult?>(null);
    }

    private static void SetCultureItems(HttpContext httpContext, string selectedCulture, string? pathCulture)
    {
        httpContext.Items[AeroCultureRoute.CultureItemKey] = selectedCulture;
        if (pathCulture is not null)
        {
            httpContext.Items[AeroCultureRoute.CulturePrefixItemKey] = pathCulture;
        }
    }
}
