using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using System.Globalization;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Defines an enumeration for ContentSlugOwnerType.
/// </summary>
public enum ContentSlugOwnerType
{
    Page = 0,
    BlogPost = 1,
    Custom = 2,
    ContentItem = 3
}

/// <summary>
/// Represents a class for ContentSlugDocument.
/// </summary>
public sealed class ContentSlugDocument : SableDocument, IAuditable, ISiteOwned
{
    private const string RootSlugKey = "__root__";

        /// <summary>
    /// Gets or sets the Site Id.
    /// </summary>
public long SiteId { get; set; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Normalized Slug.
    /// </summary>
public string NormalizedSlug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Owner Id.
    /// </summary>
public long OwnerId { get; set; } 
        /// <summary>
    /// Gets or sets the Owner Type.
    /// </summary>
    public ContentSlugOwnerType OwnerType { get; set; }

    // IAuditable
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

        /// <summary>
    /// Normalize method.
    /// </summary>
    public static string Normalize(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var segments = slug
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.ToLowerInvariant());

        return string.Join('/', segments);
    }


        /// <summary>
    /// Create method.
    /// </summary>
public static ContentSlugDocument Create(
        string slug,
        long ownerId,
        ContentSlugOwnerType ownerType,
        long siteId,
        string culture = SitesModel.DefaultCultureName)
    {
        var normalizedSlug = Normalize(slug);

        return new ContentSlugDocument
        {
            Id = Snowflake.NewId(),
            Slug = slug,
            NormalizedSlug = normalizedSlug,
            OwnerId = ownerId,
            OwnerType = ownerType,
            SiteId = siteId,
            Culture = NormalizeCulture(culture)
        };
    }

        /// <summary>
    /// NormalizeCulture method.
    /// </summary>
public static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return SitesModel.DefaultCultureName;

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return SitesModel.DefaultCultureName;
        }
    }
}

/// <summary>
/// Represents a class for SlugConflictException.
/// </summary>
public sealed class SlugConflictException(string slug, string existingOwnerId, string attemptedOwnerId)
    : InvalidOperationException($"Slug '{slug}' is already reserved by '{existingOwnerId}'.")
{
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public string Slug { get; } = slug;
        /// <summary>
    /// Gets or sets the Existing Owner Id.
    /// </summary>
public string ExistingOwnerId { get; } = existingOwnerId;
        /// <summary>
    /// Gets or sets the Attempted Owner Id.
    /// </summary>
public string AttemptedOwnerId { get; } = attemptedOwnerId;
}

/// <summary>
/// Represents a class for ContentSlugReservation.
/// </summary>
public static class ContentSlugReservation
{
        /// <summary>
    /// ReserveAsync method.
    /// </summary>
public static async Task ReserveAsync(
        IDocumentSession session,
        long ownerId,
        ContentSlugOwnerType ownerType,
        string slug,
        long siteId,
        string? previousSlug,
        CancellationToken cancellationToken)
        => await ReserveAsync(
            session,
            ownerId,
            ownerType,
            slug,
            siteId,
            SitesModel.DefaultCultureName,
            previousSlug,
            cancellationToken);

        /// <summary>
    /// ReserveAsync method.
    /// </summary>
public static async Task ReserveAsync(
        IDocumentSession session,
        long ownerId,
        ContentSlugOwnerType ownerType,
        string slug,
        long siteId,
        string culture,
        string? previousSlug,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = ContentSlugDocument.Normalize(slug);
        var normalizedCulture = ContentSlugDocument.NormalizeCulture(culture);
        
        // Find existing reservation for this slug within the current site.
        var existingReservation = await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(
                x => x.SiteId == siteId &&
                     x.Culture == normalizedCulture &&
                     x.NormalizedSlug == normalizedSlug,
                cancellationToken);
            
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
                    .FirstOrDefaultAsync(
                        x => x.SiteId == siteId &&
                             x.Culture == normalizedCulture &&
                             x.NormalizedSlug == normalizedPreviousSlug &&
                             x.OwnerId == ownerId,
                        cancellationToken);
                
                if (previousReservation is not null)
                {
                    session.Delete(previousReservation);
                }
            }
        }

        // Only store if we don't already have this reservation (avoiding duplicates if it's an update with same slug)
        if (existingReservation is null)
        {
            session.Store(ContentSlugDocument.Create(slug, ownerId, ownerType, siteId, normalizedCulture));
        }
    }
}
