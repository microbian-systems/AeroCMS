using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Shared.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Aero.Cms.Web.Bootstrap.Localization;

public sealed class AeroRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var site = httpContext.Features.Get<IAeroSiteSlice>();
        var selectedCulture = AeroCultureRoute.ResolveRequestCulture(
            httpContext.Request.Path,
            site?.DefaultCulture,
            site?.SupportedCultures,
            out var pathCulture);

        httpContext.Items[AeroCultureRoute.CultureItemKey] = selectedCulture;
        if (pathCulture is not null)
        {
            httpContext.Items[AeroCultureRoute.CulturePrefixItemKey] = pathCulture;
        }

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(selectedCulture, selectedCulture));
    }
}
