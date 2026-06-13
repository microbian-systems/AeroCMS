using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class ProductTranslationMapperTests
{
    [Test]
    public async Task Apply_OverlaysTranslatedProductFields()
    {
        var product = new ProductDocument
        {
            Id = 100,
            Name = "Starter Theme",
            Slug = "starter-theme",
            Description = "Default description",
            ShortDescription = "Default short description",
            Category = "themes",
            Price = 49
        };

        var translation = new ProductTranslation
        {
            ProductId = product.Id,
            Culture = "es-MX",
            Name = "Tema inicial",
            Description = "Descripcion localizada",
            ShortDescription = "Resumen localizado"
        };

        ProductTranslationMapper.Apply(product, translation);

        await Assert.That(product.Name).IsEqualTo("Tema inicial");
        await Assert.That(product.Description).IsEqualTo("Descripcion localizada");
        await Assert.That(product.ShortDescription).IsEqualTo("Resumen localizado");
        await Assert.That(product.Slug).IsEqualTo("starter-theme");
        await Assert.That(product.Category).IsEqualTo("themes");
        await Assert.That(product.Price).IsEqualTo(49);
    }

    [Test]
    public async Task Apply_FallsBackToSourceFields_WhenTranslatedFieldsAreBlankOrNull()
    {
        var product = new ProductDocument
        {
            Id = 100,
            Name = "Starter Theme",
            Slug = "starter-theme",
            Description = "Default description",
            ShortDescription = "Default short description"
        };

        var translation = new ProductTranslation
        {
            ProductId = product.Id,
            Culture = "es-MX",
            Name = " ",
            Description = null,
            ShortDescription = null
        };

        ProductTranslationMapper.Apply(product, translation);

        await Assert.That(product.Name).IsEqualTo("Starter Theme");
        await Assert.That(product.Description).IsEqualTo("Default description");
        await Assert.That(product.ShortDescription).IsEqualTo("Default short description");
    }
}
