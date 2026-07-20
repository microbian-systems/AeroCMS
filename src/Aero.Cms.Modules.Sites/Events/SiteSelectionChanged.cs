namespace Aero.Cms.Modules.Sites.Events;

/// <summary>
/// Describes a manager user's successful selection of an existing site.
/// </summary>
/// <param name="SiteId">The selected site's persistent identifier.</param>
/// <param name="UserId">The selecting user's persistent identifier.</param>
/// <param name="Timestamp">The UTC timestamp captured when the selection cookie was written.</param>
/// <remarks>
/// The admin endpoint publishes this event only when the principal exposes a parseable numeric
/// name-identifier claim; it does not independently require an authenticated identity.
/// Publication occurs after the response cookie is appended and is used for audit logging.
/// </remarks>
public sealed record SiteSelectionChanged(
    long SiteId,
    long UserId,
    DateTimeOffset Timestamp);
