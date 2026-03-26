namespace Aero.Cms.Core.Audit;

/// <summary>
/// Audit events for Page content operations.
/// </summary>
public sealed record PageCreatedEvent : AuditEvent
{
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required PageKind Kind { get; init; }

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

public sealed record PageUpdatedEvent : AuditEvent
{
    public string? Title { get; init; }
    public string? Slug { get; init; }

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

public sealed record PagePublishedEvent : AuditEvent
{
    public DateTimeOffset PublishedOn { get; init; }

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

public sealed record PageUnpublishedEvent : AuditEvent
{
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

public sealed record PageDeletedEvent : AuditEvent
{
    public required string Title { get; init; }

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
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public long? AuthorId { get; init; }

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

public sealed record BlogPostUpdatedEvent : AuditEvent
{
    public string? Title { get; init; }
    public string? Slug { get; init; }

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

public sealed record BlogPostPublishedEvent : AuditEvent
{
    public DateTimeOffset PublishedOn { get; init; }

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

public sealed record BlogPostUnpublishedEvent : AuditEvent
{
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

public sealed record BlogPostDeletedEvent : AuditEvent
{
    public required string Title { get; init; }

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
