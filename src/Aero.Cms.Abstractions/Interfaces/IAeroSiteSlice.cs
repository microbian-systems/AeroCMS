namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Attached to <see cref="Microsoft.AspNetCore.Http.HttpContext.Features"/> by
/// the site resolution middleware to communicate the current site to
/// downstream middleware and services.
/// </summary>
public interface IAeroSiteSlice
{
    long SiteId { get; }
    long TenantId { get; }
    string? DefaultCulture { get; }
    IReadOnlyList<string> SupportedCultures { get; }
}

/// <summary>
/// Default implementation of <see cref="IAeroSiteSlice"/>.
/// </summary>
public sealed class AeroSiteSlice : IAeroSiteSlice
{
    public long SiteId { get; init; }
    public long TenantId { get; init; }
    public string? DefaultCulture { get; init; }
    public IReadOnlyList<string> SupportedCultures { get; init; } = [];
}
