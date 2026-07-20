using Aero.Core.Data;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using System.Globalization;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Identifies the kind of content that owns a slug reservation.
/// </summary>
public enum ContentSlugOwnerType
{
    /// <summary>A CMS page owns the slug.</summary>
    Page = 0,
    /// <summary>A blog post owns the slug.</summary>
    BlogPost = 1,
    /// <summary>A custom content integration owns the slug.</summary>
    Custom = 2,
    /// <summary>A typed content item owns the slug.</summary>
    ContentItem = 3
}

/// <summary>
/// Stores a site- and culture-scoped slug reservation in Sable.
/// </summary>
/// <remarks>
/// Uniqueness is configured over site, normalized culture, and normalized slug.
/// Callers must save the containing session to persist reservations.
/// </remarks>
public sealed class ContentSlugDocument : SableDocument, IAuditable, ISiteOwned
{
    private const string RootSlugKey = "__root__";

    /// <summary>
    /// Gets or sets the site that owns the reservation.
    /// </summary>
public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the normalized culture name used for uniqueness.
    /// </summary>
public string Culture { get; set; } = SitesModel.DefaultCultureName;
    /// <summary>
    /// Gets or sets the caller-provided slug retained for display or diagnostics.
    /// </summary>
public string Slug { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the slash-separated, lower-case slug used for comparisons.
    /// </summary>
public string NormalizedSlug { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the identifier of the owning content record.
    /// </summary>
public long OwnerId { get; set; } 
    /// <summary>
    /// Gets or sets the kind of content that owns the slug.
    /// </summary>
    public ContentSlugOwnerType OwnerType { get; set; }

    // IAuditable
    /// <inheritdoc />
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    /// <inheritdoc />
    public DateTimeOffset? ModifiedOn { get; set; }
    /// <inheritdoc />
    public string? CreatedBy { get; set; }
    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Produces the comparison key for a slug or hierarchical path.
    /// </summary>
    /// <param name="slug">The slug or slash-separated path to normalize.</param>
    /// <returns>
    /// Lower-case, non-empty path segments joined by <c>/</c>, without leading or trailing slashes.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="slug"/> is <see langword="null"/>.</exception>
    public static string Normalize(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var segments = slug
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.ToLowerInvariant());

        return string.Join('/', segments);
    }


    /// <summary>
    /// Creates an unpersisted slug reservation with a new Snowflake identifier.
    /// </summary>
    /// <param name="slug">The slug or hierarchical path to reserve.</param>
    /// <param name="ownerId">The owning content identifier.</param>
    /// <param name="ownerType">The owning content kind.</param>
    /// <param name="siteId">The site in which the slug must be unique.</param>
    /// <param name="culture">The culture in which the slug must be unique.</param>
    /// <returns>A new normalized reservation document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slug"/> is <see langword="null"/>.</exception>
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
    /// Normalizes a culture name for reservation comparisons.
    /// </summary>
    /// <param name="culture">The culture name to normalize.</param>
    /// <returns>
    /// The canonical <see cref="CultureInfo.Name"/> when valid; otherwise the site default culture.
    /// </returns>
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
/// Reports an attempt to reserve a slug already owned by different content.
/// </summary>
/// <param name="slug">The conflicting slug as supplied by the caller.</param>
/// <param name="existingOwnerId">The identifier of the current owner.</param>
/// <param name="attemptedOwnerId">The identifier that attempted the reservation.</param>
public sealed class SlugConflictException(string slug, string existingOwnerId, string attemptedOwnerId)
    : InvalidOperationException($"Slug '{slug}' is already reserved by '{existingOwnerId}'.")
{
    /// <summary>
    /// Gets the conflicting slug.
    /// </summary>
public string Slug { get; } = slug;
    /// <summary>
    /// Gets the current owner's identifier.
    /// </summary>
public string ExistingOwnerId { get; } = existingOwnerId;
    /// <summary>
    /// Gets the identifier that attempted the reservation.
    /// </summary>
public string AttemptedOwnerId { get; } = attemptedOwnerId;
}

/// <summary>
/// Stages culture-aware slug reservation changes in a Sable document session.
/// </summary>
public static class ContentSlugReservation
{
    /// <summary>
    /// Reserves a slug in the default culture.
    /// </summary>
    /// <param name="session">The session in which reservation writes are staged.</param>
    /// <param name="ownerId">The owning content identifier.</param>
    /// <param name="ownerType">The owning content kind.</param>
    /// <param name="slug">The slug to reserve.</param>
    /// <param name="siteId">The site scope.</param>
    /// <param name="previousSlug">The owner's prior slug, if it should be released.</param>
    /// <param name="cancellationToken">The token used for reservation queries.</param>
    /// <returns>A task that completes after reservation changes have been staged.</returns>
    /// <exception cref="SlugConflictException">The normalized slug is reserved by another owner.</exception>
    /// <remarks>This method does not save the session.</remarks>
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
    /// Reserves a slug within a site and normalized culture.
    /// </summary>
    /// <param name="session">The session in which reservation writes are staged.</param>
    /// <param name="ownerId">The owning content identifier.</param>
    /// <param name="ownerType">The owning content kind.</param>
    /// <param name="slug">The slug to reserve.</param>
    /// <param name="siteId">The site scope.</param>
    /// <param name="culture">The culture scope; invalid values use the site default culture.</param>
    /// <param name="previousSlug">The owner's prior slug, if it should be released.</param>
    /// <param name="cancellationToken">The token used for reservation queries.</param>
    /// <returns>A task that completes after reservation changes have been staged.</returns>
    /// <exception cref="SlugConflictException">The normalized slug is reserved by another owner.</exception>
    /// <remarks>
    /// The previous reservation is deleted only when its normalized value differs
    /// from the new slug. This method does not save the session.
    /// </remarks>
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
