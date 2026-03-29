namespace Aero.Cms.Core.Messaging;

/// <summary>
/// Event fired when a content's slug has been updated and published.
/// </summary>
public record SlugUpdated(
    long ContentId, 
    string ContentType, 
    string NewSlug, 
    string? OldSlug = null);
