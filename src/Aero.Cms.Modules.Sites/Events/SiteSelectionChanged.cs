namespace Aero.Cms.Modules.Sites.Events;

/// <summary>
/// Published when a user selects a site in the manager (via POST /api/v1/admin/sites/current).
/// Wolverine handler processes this for audit logging.
/// </summary>
public sealed record SiteSelectionChanged(
    long SiteId,
    long UserId,
    DateTimeOffset Timestamp);
