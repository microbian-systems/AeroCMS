namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Immutable operation context used to scope <see cref="DocsContentService"/>
/// calls to a specific site and actor.  The caller (grain boundary or HTTP
/// boundary) is responsible for resolving the current site and user identity;
/// the service never touches <see cref="IHttpContextAccessor"/>.
/// </summary>
public sealed record DocsOperationContext(
    long SiteId,
    string Actor = "system");
