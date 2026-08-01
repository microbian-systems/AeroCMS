namespace Aero.Cms.Abstractions.Audit;

/// <summary>
/// Audit events for Page content operations.
/// </summary>
public sealed record PageCreatedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public required string Title { get; init; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public required string Slug { get; init; }
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public required PageKind Kind { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static PageCreatedEvent Create(long userId, long pageId, string title, string slug, PageKind kind) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Created,
            EntityType = AuditEntityTypes.Page,
            EntityId = pageId,
            Title = title,
            Slug = slug,
            Kind = kind
        };
}

/// <summary>
/// Represents a record for PageUpdatedEvent.
/// </summary>
public sealed record PageUpdatedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; init; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string? Slug { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static PageUpdatedEvent Create(long userId, long pageId, string? title = null, string? slug = null) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Updated,
            EntityType = AuditEntityTypes.Page,
            EntityId = pageId,
            Title = title,
            Slug = slug
        };
}

/// <summary>
/// Represents a record for PagePublishedEvent.
/// </summary>
public sealed record PagePublishedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
public DateTimeOffset PublishedOn { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static PagePublishedEvent Create(long userId, long pageId, DateTimeOffset publishedOn) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Published,
            EntityType = AuditEntityTypes.Page,
            EntityId = pageId,
            PublishedOn = publishedOn
        };
}

/// <summary>
/// Represents a record for PageUnpublishedEvent.
/// </summary>
public sealed record PageUnpublishedEvent : AuditEvent
{
        /// <summary>
    /// Create method.
    /// </summary>
public static PageUnpublishedEvent Create(long userId, long pageId) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Unpublished,
            EntityType = AuditEntityTypes.Page,
            EntityId = pageId
        };
}

/// <summary>
/// Represents a record for PageDeletedEvent.
/// </summary>
public sealed record PageDeletedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public required string Title { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static PageDeletedEvent Create(long userId, long pageId, string title) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Deleted,
            EntityType = AuditEntityTypes.Page,
            EntityId = pageId,
            Title = title
        };
}

/// <summary>
/// Page kind enumeration (mirrored from PageDocument for convenience).
/// </summary>
public enum PageKind
{
    Standard = 0,
    Homepage = 1,
    BlogListing = 2,
    Custom = 3
}

/// <summary>
/// Audit events for BlogPost content operations.
/// </summary>
public sealed record BlogPostCreatedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public required string Title { get; init; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public required string Slug { get; init; }
        /// <summary>
    /// Gets or sets the Author Id.
    /// </summary>
public long? AuthorId { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static BlogPostCreatedEvent Create(long userId, long blogPostId, string title, string slug, long? authorId = null) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Created,
            EntityType = AuditEntityTypes.BlogPost,
            EntityId = blogPostId,
            Title = title,
            Slug = slug,
            AuthorId = authorId
        };
}

/// <summary>
/// Represents a record for BlogPostUpdatedEvent.
/// </summary>
public sealed record BlogPostUpdatedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string? Title { get; init; }
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string? Slug { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static BlogPostUpdatedEvent Create(long userId, long blogPostId, string? title = null, string? slug = null) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Updated,
            EntityType = AuditEntityTypes.BlogPost,
            EntityId = blogPostId,
            Title = title,
            Slug = slug
        };
}

/// <summary>
/// Represents a record for BlogPostPublishedEvent.
/// </summary>
public sealed record BlogPostPublishedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Published On.
    /// </summary>
public DateTimeOffset PublishedOn { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static BlogPostPublishedEvent Create(long userId, long blogPostId, DateTimeOffset publishedOn) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Published,
            EntityType = AuditEntityTypes.BlogPost,
            EntityId = blogPostId,
            PublishedOn = publishedOn
        };
}

/// <summary>
/// Represents a record for BlogPostUnpublishedEvent.
/// </summary>
public sealed record BlogPostUnpublishedEvent : AuditEvent
{
        /// <summary>
    /// Create method.
    /// </summary>
public static BlogPostUnpublishedEvent Create(long userId, long blogPostId) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Unpublished,
            EntityType = AuditEntityTypes.BlogPost,
            EntityId = blogPostId
        };
}

/// <summary>
/// Represents a record for BlogPostDeletedEvent.
/// </summary>
public sealed record BlogPostDeletedEvent : AuditEvent
{
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public required string Title { get; init; }

        /// <summary>
    /// Create method.
    /// </summary>
public static BlogPostDeletedEvent Create(long userId, long blogPostId, string title) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = userId,
            EventType = AuditEventTypes.Deleted,
            EntityType = AuditEntityTypes.BlogPost,
            EntityId = blogPostId,
            Title = title
        };
}
