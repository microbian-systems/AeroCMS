namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Carries a site and actor value for documentation operations whose consumers choose to
/// accept this context.
/// </summary>
/// <remarks>
/// The current <see cref="DocsContentService"/> does not consume this record; it resolves site
/// scope from <see cref="Aero.Core.Http.ISiteContext"/> and receives actor values separately.
/// </remarks>
/// <param name="SiteId">The site identifier made available to a consumer.</param>
/// <param name="Actor">The audit actor name; defaults to <c>system</c>.</param>
public sealed record DocsOperationContext(
    long SiteId,
    string Actor = "system");
