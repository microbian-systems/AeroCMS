using Aero.Cms.Modules.Sites.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Sites.Handlers;

/// <summary>
/// Wolverine handler that logs site selections for audit purposes.
/// </summary>
[WolverineHandler]
public sealed class SiteSelectionAuditHandler(
    ILogger<SiteSelectionAuditHandler> log) : IWolverineHandler
{
    public void Handle(SiteSelectionChanged e)
    {
        log.LogInformation("User {UserId} selected site {SiteId} at {Timestamp}",
            e.UserId, e.SiteId, e.Timestamp);
    }
}
