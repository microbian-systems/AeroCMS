using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Modules.Content;
using Aero.Cms.Modules.Content.Caching;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentHierarchyManagerServiceTests
{
    [Test]
    public async Task Tree_is_site_and_culture_scoped_and_pre_shaped()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>();
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(10, "Root", sortOrder: 0),
            Item(11, "Second", parentId: 10, sortOrder: 20),
            Item(12, "First", parentId: 10, sortOrder: 10),
            Item(13, "Other site", siteId: 2),
            Item(14, "French", culture: "fr-FR"));
        await harness.Session.SaveChangesAsync();

        var service = CreateService(harness, Definition());
        var result = await service.GetTreeAsync("category", "en-US");

        var ok = result as Result<ContentHierarchyTreeResult>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.TotalCount).IsEqualTo(3);
        await Assert.That(ok.Value.Roots.Count).IsEqualTo(1);
        await Assert.That(ok.Value.Roots[0].Children.Select(child => child.Id))
            .IsEquivalentTo([12L, 11L]);
    }

    [Test]
    public async Task Move_reparents_and_normalizes_both_sibling_collections_atomically()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>();
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(20, "First root", sortOrder: 0),
            Item(21, "Second root", sortOrder: 1),
            Item(22, "Existing child", parentId: 21, sortOrder: 7));
        await harness.Session.SaveChangesAsync();

        var service = CreateService(harness, Definition());
        var result = await service.MoveAsync(
            "category",
            20,
            new MoveContentItemRequest(21, 0, "en-US"));

        await Assert.That(result.IsSuccess).IsTrue();
        await using var verification = await harness.OpenSessionAsync();
        var moved = await verification.LoadAsync<ContentItem>(20);
        var existing = await verification.LoadAsync<ContentItem>(22);
        var remainingRoot = await verification.LoadAsync<ContentItem>(21);
        await Assert.That(moved!.ParentId).IsEqualTo(21);
        await Assert.That(moved.SortOrder).IsEqualTo(0);
        await Assert.That(existing!.SortOrder).IsEqualTo(1);
        await Assert.That(remainingRoot!.SortOrder).IsEqualTo(0);
    }

    [Test]
    public async Task Move_fails_closed_for_an_item_owned_by_another_site()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>();
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(30, "Local root"),
            Item(31, "Foreign item", siteId: 2));
        await harness.Session.SaveChangesAsync();

        var service = CreateService(harness, Definition());
        var result = await service.MoveAsync(
            "category",
            31,
            new MoveContentItemRequest(30, 0, "en-US"));

        await Assert.That(result.IsFailure).IsTrue();
        await using var verification = await harness.OpenSessionAsync();
        var foreign = await verification.LoadAsync<ContentItem>(31);
        await Assert.That(foreign!.ParentId).IsNull();
    }

    [Test]
    public async Task Reorder_changes_only_the_target_content_type_partition()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>();
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(40, "First category", sortOrder: 0),
            Item(41, "Second category", sortOrder: 1),
            Item(42, "Unrelated promo", sortOrder: 77, contentTypeAlias: "promo"));
        await harness.Session.SaveChangesAsync();

        var service = CreateService(harness, Definition());
        var result = await service.ReorderAsync(
            "category",
            new ReorderContentSiblingsRequest(null, [41, 40], "en-US"));

        await Assert.That(result.IsSuccess).IsTrue();
        await using var verification = await harness.OpenSessionAsync();
        await Assert.That((await verification.LoadAsync<ContentItem>(41))!.SortOrder)
            .IsEqualTo(0);
        await Assert.That((await verification.LoadAsync<ContentItem>(40))!.SortOrder)
            .IsEqualTo(1);
        await Assert.That((await verification.LoadAsync<ContentItem>(42))!.SortOrder)
            .IsEqualTo(77);
    }

    private static ContentHierarchyManagerService CreateService(
        SableTestHarness harness,
        ContentTypeDefinition definition)
    {
        var typeService = Substitute.For<IContentTypeService>();
        typeService.GetByAliasAsync(
                definition.SiteId,
                definition.Alias,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(definition)));
        typeService.GetByIdAsync(
                definition.SiteId,
                definition.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(definition)));

        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(definition.SiteId);
        var invalidator = new ContentCacheInvalidator(
            Substitute.For<IFusionCache>(),
            Substitute.For<IOutputCacheStore>(),
            NullLogger<ContentCacheInvalidator>.Instance);

        return new ContentHierarchyManagerService(
            harness.Session,
            typeService,
            new ContentHierarchyValidator(harness.Session, typeService),
            siteContext,
            invalidator,
            NullLogger<ContentHierarchyManagerService>.Instance);
    }

    private static ContentTypeDefinition Definition() => new()
    {
        Id = 1,
        SiteId = 1,
        Alias = "category",
        Name = "Category",
        Structure = ContentStructure.Hierarchical,
        HierarchyRules = new ContentHierarchyRules
        {
            AllowRootItems = true,
            RequireSameTypeParent = true,
            MaximumDepth = 8
        }
    };

    private static ContentItem Item(
        long id,
        string title,
        long siteId = 1,
        string culture = "en-US",
        long? parentId = null,
        int sortOrder = 0,
        string contentTypeAlias = "category") => new()
    {
        Id = id,
        SiteId = siteId,
        ContentTypeAlias = contentTypeAlias,
        Culture = culture,
        Title = title,
        Slug = title.ToLowerInvariant().Replace(' ', '-'),
        ParentId = parentId,
        SortOrder = sortOrder,
        PublicationState = ContentPublicationState.Draft
    };
}
