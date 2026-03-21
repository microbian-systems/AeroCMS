using Marten;

namespace Aero.Cms.Modules.Pages;

public enum ContentSlugOwnerType
{
    Page = 0,
    BlogPost = 1
}

public sealed class ContentSlugDocument
{
    private const string RootSlugKey = "__root__";

    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string NormalizedSlug { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public ContentSlugOwnerType OwnerType { get; set; }

    public static string Normalize(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var segments = slug
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.ToLowerInvariant());

        return string.Join('/', segments);
    }

    public static string BuildDocumentId(string slug)
    {
        var normalizedSlug = Normalize(slug);
        return $"cms/slugs/{(string.IsNullOrWhiteSpace(normalizedSlug) ? RootSlugKey : normalizedSlug)}";
    }

    public static ContentSlugDocument Create(string slug, string ownerId, ContentSlugOwnerType ownerType)
    {
        var normalizedSlug = Normalize(slug);

        return new ContentSlugDocument
        {
            Id = BuildDocumentId(slug),
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
        string ownerId,
        ContentSlugOwnerType ownerType,
        string slug,
        string? previousSlug,
        CancellationToken cancellationToken)
    {
        var slugDocumentId = ContentSlugDocument.BuildDocumentId(slug);
        var existingReservation = await session.LoadAsync<ContentSlugDocument>(slugDocumentId, cancellationToken);
        if (existingReservation is not null && !string.Equals(existingReservation.OwnerId, ownerId, StringComparison.Ordinal))
        {
            throw new SlugConflictException(slug, existingReservation.OwnerId, ownerId);
        }

        var previousSlugDocumentId = string.IsNullOrWhiteSpace(previousSlug)
            ? null
            : ContentSlugDocument.BuildDocumentId(previousSlug);

        if (previousSlugDocumentId is not null && !string.Equals(previousSlugDocumentId, slugDocumentId, StringComparison.Ordinal))
        {
            var previousReservation = await session.LoadAsync<ContentSlugDocument>(previousSlugDocumentId, cancellationToken);
            if (previousReservation is not null &&
                string.Equals(previousReservation.OwnerId, ownerId, StringComparison.Ordinal) &&
                previousReservation.OwnerType == ownerType)
            {
                session.Delete(previousReservation);
            }
        }

        session.Store(ContentSlugDocument.Create(slug, ownerId, ownerType));
    }
}
