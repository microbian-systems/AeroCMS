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
        /// <summary>
    /// Created.
    /// </summary>
public const string Created = "Created";
        /// <summary>
    /// Updated.
    /// </summary>
public const string Updated = "Updated";
        /// <summary>
    /// Published.
    /// </summary>
public const string Published = "Published";
        /// <summary>
    /// Unpublished.
    /// </summary>
public const string Unpublished = "Unpublished";
        /// <summary>
    /// Deleted.
    /// </summary>
public const string Deleted = "Deleted";
}

/// <summary>
/// Common entity type constants.
/// </summary>
public static class AuditEntityTypes
{
        /// <summary>
    /// Page.
    /// </summary>
public const string Page = "Page";
        /// <summary>
    /// BlogPost.
    /// </summary>
public const string BlogPost = "BlogPost";
}
