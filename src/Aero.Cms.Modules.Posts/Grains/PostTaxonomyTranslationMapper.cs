using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Posts.Grains;

internal static class PostTaxonomyTranslationMapper
{
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
