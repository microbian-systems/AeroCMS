namespace Aero.Cms.Abstractions.Audit;

/// <summary>
/// Base class for all CMS audit events containing common properties.
/// </summary>
public abstract record AuditEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the ID of the user who triggered the event.
    /// </summary>
    public required long UserId { get; init; }

    /// <summary>
    /// Gets the type of the event (e.g., "Created", "Updated", "Deleted").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Gets the type of the entity affected (e.g., "Page", "BlogPost").
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets the ID of the entity affected.
    /// </summary>
    public required long EntityId { get; init; }

    /// <summary>
    /// Gets optional metadata associated with the event.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Common event type constants.
/// </summary>
public static class AuditEventTypes
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Published = "Published";
    public const string Unpublished = "Unpublished";
    public const string Deleted = "Deleted";
}

/// <summary>
/// Common entity type constants.
/// </summary>
public static class AuditEntityTypes
{
    public const string Page = "Page";
    public const string BlogPost = "BlogPost";
}
