using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Modules.Content.Caching;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentCacheScopeTests
{
    [Test]
    public async Task Poisoned_item_keys_reload_selected_site_and_same_slug_isolated_per_site()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 1, Alias = "article", Name = "Article" },
            new ContentTypeDocument { Id = 11, SiteId = 2, Alias = "article", Name = "Article" });
        harness.Session.Store(
            new ContentItem
            {
                Id = 20,
                SiteId = 1,
                ContentTypeAlias = "article",
                Title = "Local",
                Slug = "same-slug",
                Culture = "en-US"
            },
            new ContentItem
            {
                Id = 21,
                SiteId = 2,
                ContentTypeAlias = "article",
                Title = "Foreign",
                Slug = "same-slug",
                Culture = "en-US"
            });
        await harness.Session.SaveChangesAsync();
        await using var cacheProvider = CreateCacheProvider();
        var cache = cacheProvider.GetRequiredService<IFusionCache>();
        var service = CreateContentService(harness.Session, cache);
        var wrongId = new ContentItem
        {
            Id = 999,
            SiteId = 1,
            ContentTypeAlias = "article",
            Title = "Wrong identifier",
            Slug = "wrong-id",
            Culture = "en-US"
        };
        var foreign = new ContentItem
        {
            Id = 21,
            SiteId = 2,
            ContentTypeAlias = "article",
            Title = "Poison",
            Slug = "same-slug",
            Culture = "en-US"
        };
        await cache.SetAsync(ContentCacheKeys.ItemById(1, 20), wrongId);
        await cache.SetAsync(ContentCacheKeys.ItemById(1, 777), wrongId);
        await cache.SetAsync(ContentCacheKeys.ItemBySlug(1, "same-slug"), foreign);
        await cache.SetAsync(
            ContentCacheKeys.ItemByTypedSlug(1, "article", "en-US", "same-slug"),
            foreign);

        var byId = Unwrap(await service.LoadAsync(1, 20));
        var poisonedMissingExists = await service.ExistsAsync(1, 777);
        var bySlug = Unwrap(await service.GetBySlugAsync(1, "same-slug"));
        var byTypedSlug = Unwrap(
            await service.GetBySlugAndTypeAsync(1, "article", "en-US", "same-slug"));
        var otherSite = Unwrap(await service.GetBySlugAsync(2, "same-slug"));

        await Assert.That(new[] { byId, bySlug, byTypedSlug }.All(item =>
            item.SiteId == 1 && item.Id == 20 && item.Title == "Local")).IsTrue();
        await Assert.That(poisonedMissingExists).IsFalse();
        await Assert.That(otherSite.SiteId).IsEqualTo(2);
        await Assert.That(otherSite.Id).IsEqualTo(21);
    }

    [Test]
    public async Task Poisoned_type_alias_and_mixed_site_list_are_rejected_and_reloaded()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument { Id = 30, SiteId = 1, Alias = "article", Name = "Local" },
            new ContentTypeDocument { Id = 31, SiteId = 2, Alias = "article", Name = "Foreign" });
        await harness.Session.SaveChangesAsync();
        await using var cacheProvider = CreateCacheProvider();
        var cache = cacheProvider.GetRequiredService<IFusionCache>();
        var service = CreateContentTypeService(harness.Session, cache);
        await cache.SetAsync(
            ContentCacheKeys.TypeByAlias(1, "article"),
            new ContentTypeDefinition
            {
                Id = 31,
                SiteId = 2,
                Alias = "article",
                Name = "Poison"
            });
        await cache.SetAsync(
            ContentCacheKeys.TypeList(1),
            new CachedContentTypeService.ContentTypeListCacheEntry(
            [
                new ContentTypeDefinition
                {
                    Id = 30,
                    SiteId = 1,
                    Alias = "article",
                    Name = "Cached local"
                },
                new ContentTypeDefinition
                {
                    Id = 31,
                    SiteId = 2,
                    Alias = "article",
                    Name = "Cached foreign"
                }
            ]));

        var byAlias = Unwrap(await service.GetByAliasAsync(1, "article"));
        var list = Unwrap(await service.GetAllAsync(1));

        await Assert.That(byAlias.SiteId).IsEqualTo(1);
        await Assert.That(byAlias.Id).IsEqualTo(30);
        await Assert.That(byAlias.Name).IsEqualTo("Local");
        await Assert.That(list).HasSingleItem();
        await Assert.That(list[0].SiteId).IsEqualTo(1);
        await Assert.That(list[0].Name).IsEqualTo("Local");
    }

    private static ServiceProvider CreateCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddFusionCache();
        return services.BuildServiceProvider();
    }

    private static CachedContentService CreateContentService(
        IDocumentSession session,
        IFusionCache cache)
    {
        var invalidator = new ContentCacheInvalidator(
            cache,
            Substitute.For<IOutputCacheStore>(),
            NullLogger<ContentCacheInvalidator>.Instance);
        return new CachedContentService(
            new AeroContentService(session),
            cache,
            invalidator,
            NullLogger<CachedContentService>.Instance);
    }

    private static CachedContentTypeService CreateContentTypeService(
        IDocumentSession session,
        IFusionCache cache)
    {
        var invalidator = new ContentCacheInvalidator(
            cache,
            Substitute.For<IOutputCacheStore>(),
            NullLogger<ContentCacheInvalidator>.Instance);
        return new CachedContentTypeService(
            new AeroContentTypeService(session, [], new ScribanTemplateValidator()),
            cache,
            invalidator,
            NullLogger<CachedContentTypeService>.Instance);
    }

    private static T Unwrap<T>(Result<T, AeroError> result) =>
        result is Result<T, AeroError>.Ok ok
            ? ok.Value
            : throw new InvalidOperationException("Expected a successful result.");
}
