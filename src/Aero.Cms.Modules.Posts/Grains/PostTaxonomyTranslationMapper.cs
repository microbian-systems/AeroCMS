using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Posts.Grains;

/// <summary>
/// Projects persisted taxonomy documents into actor view models with optional culture overlays.
/// </summary>
internal static class PostTaxonomyTranslationMapper
{
    /// <summary>
    /// Maps a category, preferring a nonblank translated name and slug and a non-null description.
    /// </summary>
    /// <param name="category">The base category document.</param>
    /// <param name="translation">The optional translation for the requested culture.</param>
    /// <returns>A view model retaining the base identity, ownership, hierarchy, and audit fields.</returns>
public static CategoryViewModel MapCategory(Models.Category category, CategoryTranslation? translation = null) => new()
    {
        Id = category.Id,
        SiteId = category.SiteId,
        Name = string.IsNullOrWhiteSpace(translation?.Name) ? category.Name : translation.Name,
        Slug = string.IsNullOrWhiteSpace(translation?.Slug) ? category.Slug : translation.Slug,
        Description = translation?.Description ?? category.Description,
        ParentCategoryId = category.ParentCategoryId,
        CreatedOn = category.CreatedOn,
        ModifiedOn = category.ModifiedOn,
        CreatedBy = category.CreatedBy,
        ModifiedBy = category.ModifiedBy
    };

    /// <summary>
    /// Maps a tag, overlaying its display name and description while retaining the base slug.
    /// </summary>
    /// <param name="tag">The base tag document.</param>
    /// <param name="translation">The optional translation for the requested culture.</param>
    /// <returns>A view model retaining the base identity, ownership, slug, and audit fields.</returns>
public static TagViewModel MapTag(Models.Tag tag, TagTranslation? translation = null) => new()
    {
        Id = tag.Id,
        SiteId = tag.SiteId,
        Name = string.IsNullOrWhiteSpace(translation?.Name) ? tag.Name : translation.Name,
        Slug = tag.Slug,
        Description = translation?.Description,
        CreatedOn = tag.CreatedOn,
        ModifiedOn = tag.ModifiedOn,
        CreatedBy = tag.CreatedBy,
        ModifiedBy = tag.ModifiedBy
    };

    /// <summary>
    /// Maps a series, preferring a nonblank translated name and slug and a non-null description.
    /// </summary>
    /// <param name="series">The base series document.</param>
    /// <param name="translation">The optional translation for the requested culture.</param>
    /// <returns>A view model retaining the base identity, ownership, and audit fields.</returns>
public static SeriesViewModel MapSeries(Models.Series series, SeriesTranslation? translation = null) => new()
    {
        Id = series.Id,
        SiteId = series.SiteId,
        Name = string.IsNullOrWhiteSpace(translation?.Name) ? series.Name : translation.Name,
        Slug = string.IsNullOrWhiteSpace(translation?.Slug) ? series.Slug : translation.Slug,
        Description = translation?.Description ?? series.Description,
        CreatedOn = series.CreatedOn,
        ModifiedOn = series.ModifiedOn,
        CreatedBy = series.CreatedBy,
        ModifiedBy = series.ModifiedBy
    };
}
