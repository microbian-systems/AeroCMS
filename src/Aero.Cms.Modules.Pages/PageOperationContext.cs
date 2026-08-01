namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Immutable operation context used to scope <see cref="AeroPageContentService"/>
/// calls to a specific site and actor.  The caller (grain boundary or HTTP
/// boundary) is responsible for resolving the current site and user identity;
/// the service never touches <see cref="IHttpContextAccessor"/>.
/// </summary>
/// <param name="SiteId">The site whose pages may be accessed.</param>
/// <param name="Actor">The audit actor name used by page mutations.</param>
public sealed record PageOperationContext(
    long SiteId,
    string Actor = "system");
