using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Shared.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Aero.Cms.Web.Bootstrap.Localization;

/// <summary>
/// Selects a request culture from the URL prefix and the resolved site's culture configuration.
/// </summary>
/// <remarks>
/// This provider does not inspect culture cookies, query strings, or request headers. Returning
/// <see langword="null"/> allows later providers in the localization chain to evaluate those sources.
/// Selected culture values and request items are localization metadata; this provider does not authenticate
/// the request or establish site or tenant authorization.
/// </remarks>
public sealed class AeroRequestCultureProvider : RequestCultureProvider
{
    private static readonly PathString ManagerPathPrefix = "/manager";

    /// <summary>
    /// Determines the request culture from a supported path prefix or an available site slice.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context.</param>
    /// <returns>
    /// A completed task containing the selected culture for both formatting and UI when the URL has a
    /// recognized culture prefix or a site slice is available; otherwise, a task containing
    /// <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// Site cultures take precedence over the localization options. When a culture is selected, it is
    /// stored in <see cref="HttpContext.Items"/> under <see cref="AeroCultureRoute.CultureItemKey"/>;
    /// a recognized URL prefix is also stored under <see cref="AeroCultureRoute.CulturePrefixItemKey"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <see langword="null"/>.</exception>
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
