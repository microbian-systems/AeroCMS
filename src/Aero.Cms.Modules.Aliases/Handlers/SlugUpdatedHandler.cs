using Microsoft.Extensions.Logging;
using Aero.Cms.Events;
using Wolverine;

namespace Aero.Cms.Modules.Aliases.Handlers;

/// <summary>
/// Intercepts SlugUpdated messages from the Wolverine bus.
/// </summary>
public class SlugUpdatedHandler(ILogger<SlugUpdatedHandler> logger) : IWolverineHandler
{
    private readonly ILogger<SlugUpdatedHandler> _logger = logger;

    /// <summary>
    /// Handles the SlugUpdated event.
    /// Currently only logs the event as requested.
    /// </summary>
    public void Handle(SlugUpdated message)
    {
        _logger.LogInformation("SlugUpdated message intercepted for {ContentType} {ContentId}: {OldSlug} -> {NewSlug}", 
            message.ContentType, message.ContentId, message.OldSlug ?? "(none)", message.NewSlug);
            
        // Future: Update alias mappings or external search indexes
    }
}
