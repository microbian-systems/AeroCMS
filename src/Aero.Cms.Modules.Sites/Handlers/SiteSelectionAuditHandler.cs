using Aero.Cms.Modules.Sites.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Sites.Handlers;

/// <summary>
/// Writes successful manager site selections to the audit log.
/// </summary>
/// <param name="log">The structured audit logger.</param>
[WolverineHandler]
public sealed class SiteSelectionAuditHandler(
    ILogger<SiteSelectionAuditHandler> log) : IWolverineHandler
{
    /// <summary>
    /// Records the selecting user, selected site, and selection timestamp.
    /// </summary>
    /// <param name="e">The completed site-selection event.</param>
public void Handle(SiteSelectionChanged e)
    {
        log.LogInformation("User {UserId} selected site {SiteId} at {Timestamp}",
            e.UserId, e.SiteId, e.Timestamp);
    }
}
