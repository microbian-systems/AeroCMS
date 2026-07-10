using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.Catalog.Services;

internal static class ProductTranslationMapper
{
        /// <summary>
    /// Apply method.
    /// </summary>
public static void Apply(ProductDocument product, ProductTranslation translation)
    {
        if (!string.IsNullOrWhiteSpace(translation.Name))
            product.Name = translation.Name;

        if (translation.Description is not null)
            product.Description = translation.Description;

        if (translation.ShortDescription is not null)
            product.ShortDescription = translation.ShortDescription;
    }
}
