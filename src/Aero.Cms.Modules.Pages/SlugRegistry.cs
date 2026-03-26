using Aero.Core;

namespace Aero.Cms.Modules.Pages;

public enum ContentSlugOwnerType
{
    Page = 0,
    BlogPost = 1,
    Custom = 2
}

public sealed class ContentSlugDocument
{
    private const string RootSlugKey = "__root__";

    public long Id { get; set; } = Snowflake.NewId();
    public string Slug { get; set; } = string.Empty;
    public string NormalizedSlug { get; set; } = string.Empty;
    public long OwnerId { get; set; } 
    public ContentSlugOwnerType OwnerType { get; set; }

    public static string Normalize(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var segments = slug
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.ToLowerInvariant());

        return string.Join('/', segments);
    }


    public static ContentSlugDocument Create(string slug, long ownerId, ContentSlugOwnerType ownerType)
    {
        var normalizedSlug = Normalize(slug);

        return new ContentSlugDocument
        {
            Id = Snowflake.NewId(),
            Slug = slug,
            NormalizedSlug = normalizedSlug,
            OwnerId = ownerId,
            OwnerType = ownerType
        };
    }
}

public sealed class SlugConflictException(string slug, string existingOwnerId, string attemptedOwnerId)
    : InvalidOperationException($"Slug '{slug}' is already reserved by '{existingOwnerId}'.")
{
    public string Slug { get; } = slug;
    public string ExistingOwnerId { get; } = existingOwnerId;
    public string AttemptedOwnerId { get; } = attemptedOwnerId;
}

public static class ContentSlugReservation
{
    public static async Task ReserveAsync(
        IDocumentSession session,
        long ownerId,
        ContentSlugOwnerType ownerType,
        string slug,
        string? previousSlug,
        CancellationToken cancellationToken)
    {
        var slugDocumentId = Snowflake.NewId();
        var existingReservation = await session.LoadAsync<ContentSlugDocument>(slugDocumentId, cancellationToken);
        if (existingReservation is not null && !string.Equals(existingReservation.OwnerId.ToString(), ownerId.ToString(), StringComparison.Ordinal))
        {
            throw new SlugConflictException(slug, existingReservation.OwnerId.ToString(), ownerId.ToString());
        }

        var previousSlugDocumentId = string.IsNullOrWhiteSpace(previousSlug)
            ? null
            : Snowflake.NewId().ToString();

        if (previousSlugDocumentId is not null && !string.Equals(previousSlugDocumentId, slugDocumentId.ToString(), StringComparison.Ordinal))
        {
            var previousReservation = await session.LoadAsync<ContentSlugDocument>(previousSlugDocumentId, cancellationToken);
            if (previousReservation is not null &&
                previousReservation.OwnerId == ownerId &&
                previousReservation.OwnerType == ownerType)
            {
                session.Delete(previousReservation);
            }
        }

        session.Store(ContentSlugDocument.Create(slug, ownerId, ownerType));
    }
}
