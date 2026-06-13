namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Immutable operation context used to scope <see cref="MartenPageContentService"/>
/// calls to a specific site and actor.  The caller (grain boundary or HTTP
/// boundary) is responsible for resolving the current site and user identity;
/// the service never touches <see cref="IHttpContextAccessor"/>.
/// </summary>
public sealed record PageOperationContext(
    long SiteId,
    string Actor = "system");
