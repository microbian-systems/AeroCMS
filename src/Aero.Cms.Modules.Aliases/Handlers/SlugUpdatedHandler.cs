using Aero.Cms.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Aliases.Handlers;

/// <summary>
/// Observes <see cref="SlugUpdated"/> messages. The current handler only logs
/// the message and does not create aliases or update external indexes.
/// </summary>
[WolverineHandler]
public class SlugUpdatedHandler(ILogger<SlugUpdatedHandler> logger) : IWolverineHandler
{
    private readonly ILogger<SlugUpdatedHandler> _logger = logger;

    /// <summary>
    /// Logs the received message without mutating persistence or alias state.
    /// </summary>
    public void Handle(SlugUpdated message)
    {
        _logger.LogInformation("SlugUpdated message intercepted for {ContentType} {ContentId}: {OldSlug} -> {NewSlug}", 
            message.ContentType, message.ContentId, message.OldSlug ?? "(none)", message.NewSlug);
            
        // Future: Update alias mappings or external search indexes
    }
}
