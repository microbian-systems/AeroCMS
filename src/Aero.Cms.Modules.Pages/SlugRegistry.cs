using Aero.Core;
using Marten;

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
        var normalizedSlug = ContentSlugDocument.Normalize(slug);
        
        // Find existing reservation for this slug
        var existingReservation = await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x => x.NormalizedSlug == normalizedSlug, cancellationToken);
            
        if (existingReservation is not null && existingReservation.OwnerId != ownerId)
        {
            throw new SlugConflictException(slug, existingReservation.OwnerId.ToString(), ownerId.ToString());
        }

        // If we have a previous slug, remove its reservation if it's different from the new one
        if (!string.IsNullOrWhiteSpace(previousSlug))
        {
            var normalizedPreviousSlug = ContentSlugDocument.Normalize(previousSlug);
            if (normalizedPreviousSlug != normalizedSlug)
            {
                var previousReservation = await session.Query<ContentSlugDocument>()
                    .FirstOrDefaultAsync(x => x.NormalizedSlug == normalizedPreviousSlug && x.OwnerId == ownerId, cancellationToken);
                
                if (previousReservation is not null)
                {
                    session.Delete(previousReservation);
                }
            }
        }

        // Only store if we don't already have this reservation (avoiding duplicates if it's an update with same slug)
        if (existingReservation is null)
        {
            session.Store(ContentSlugDocument.Create(slug, ownerId, ownerType));
        }
    }
}
