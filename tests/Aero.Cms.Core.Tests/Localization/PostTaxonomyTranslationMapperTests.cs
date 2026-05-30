using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Posts.Grains;
using Category = Aero.Cms.Modules.Posts.Models.Category;
using Tag = Aero.Cms.Modules.Posts.Models.Tag;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class PostTaxonomyTranslationMapperTests
{
    [Test]
    public async Task MapCategory_AppliesTranslatedFields()
    {
        var category = new Category
        {
            Id = 10,
            SiteId = 42,
            Name = "Architecture",
            Slug = "architecture",
            Description = "Architecture posts",
            ParentCategoryId = 5,
            CreatedBy = "seed",
            ModifiedBy = "editor"
        };

        var translation = new CategoryTranslation
        {
            CategoryId = category.Id,
            Culture = "es-MX",
            Name = "Arquitectura",
            Slug = "arquitectura",
            Description = "Publicaciones de arquitectura"
        };

        var vm = PostTaxonomyTranslationMapper.MapCategory(category, translation);

        await Assert.That(vm.Id).IsEqualTo(category.Id);
        await Assert.That(vm.SiteId).IsEqualTo(category.SiteId);
        await Assert.That(vm.Name).IsEqualTo("Arquitectura");
        await Assert.That(vm.Slug).IsEqualTo("arquitectura");
        await Assert.That(vm.Description).IsEqualTo("Publicaciones de arquitectura");
        await Assert.That(vm.ParentCategoryId).IsEqualTo(category.ParentCategoryId);
    }

    [Test]
    public async Task MapCategory_FallsBackToSourceFields_WhenTranslationIsBlank()
    {
        var category = new Category
        {
            Id = 10,
            SiteId = 42,
            Name = "Design",
            Slug = "design",
            Description = "Design posts"
        };

        var translation = new CategoryTranslation
        {
            CategoryId = category.Id,
            Culture = "es-MX",
            Name = " ",
            Slug = "",
            Description = null
        };

        var vm = PostTaxonomyTranslationMapper.MapCategory(category, translation);

        await Assert.That(vm.Name).IsEqualTo("Design");
        await Assert.That(vm.Slug).IsEqualTo("design");
        await Assert.That(vm.Description).IsEqualTo("Design posts");
    }

    [Test]
    public async Task MapTag_AppliesTranslatedDisplayFields_ButKeepsCanonicalSlug()
    {
        var tag = new Tag
        {
            Id = 20,
            SiteId = 42,
            Name = "Performance",
            Slug = "performance"
        };

        var translation = new TagTranslation
        {
            TagId = tag.Id,
            Culture = "es-MX",
            Name = "Rendimiento",
            Description = "Etiqueta para rendimiento"
        };

        var vm = PostTaxonomyTranslationMapper.MapTag(tag, translation);

        await Assert.That(vm.Name).IsEqualTo("Rendimiento");
        await Assert.That(vm.Slug).IsEqualTo("performance");
        await Assert.That(vm.Description).IsEqualTo("Etiqueta para rendimiento");
    }

    [Test]
    public async Task MapTag_FallsBackToSourceName_WhenTranslationNameIsBlank()
    {
        var tag = new Tag
        {
            Id = 20,
            SiteId = 42,
            Name = "CMS",
            Slug = "cms"
        };

        var translation = new TagTranslation
        {
            TagId = tag.Id,
            Culture = "es-MX",
            Name = " ",
            Description = null
        };

        var vm = PostTaxonomyTranslationMapper.MapTag(tag, translation);

        await Assert.That(vm.Name).IsEqualTo("CMS");
        await Assert.That(vm.Slug).IsEqualTo("cms");
        await Assert.That(vm.Description).IsNull();
    }
}
