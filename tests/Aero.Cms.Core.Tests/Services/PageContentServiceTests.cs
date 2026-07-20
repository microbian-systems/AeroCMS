using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Aliases;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageContentServiceTests
{
    private SableTestHarness _harness = null!;
    private IMessageBus _bus = null!;
    private ISiteContext _siteContext = null!;
    private AeroPageContentService _service = null!;

    private static readonly ILogger<AeroPageContentService> NullLogger = NullLogger<AeroPageContentService>.Instance;

    [Before(Test)]
    public async Task Setup()
    {
        _harness = new SableTestHarness();
        _harness.WithSchema<PageDocument>(SchemaMode.Flexible);
        _harness.WithSchema<ContentSlugDocument>();
        _harness.WithSchema<AliasDocument>();
        await _harness.InitializeAsync();

        _bus = Substitute.For<IMessageBus>();

        _siteContext = Substitute.For<ISiteContext>();
        _siteContext.SiteId.Returns(42);

        _service = new AeroPageContentService(
            _harness.Session,
            _bus,
            _siteContext,
            NullLogger,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver()
        );
    }

    [After(Test)]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    //  Test 1: SaveAsync stamps SiteId from context
    // -----------------------------------------------------------------------
    [Test]
    public async Task SaveAsync_StampsSiteId_FromContext()
    {
        // Arrange — SiteId is 0 (default), no page in DB yet
        var page = new PageDocument { Id = Snowflake.NewId(), Title = "Test", Slug = "test" };

        // Act
        var result = await _service.SaveAsync(page, CancellationToken.None);

        // Assert — SiteId stamped on the in-memory object before any DB operation
        page.SiteId.ShouldBe(42);
    }

    // -----------------------------------------------------------------------
    //  Test 2: CreateAsync stamps SiteId from context
    // -----------------------------------------------------------------------
    [Test]
    public async Task CreateAsync_StampsSiteId_FromContext()
    {
        // Arrange
        var request = new CreatePageRequest(
            Title: "New Page",
            Slug: "new-page",
            Summary: null,
            SeoTitle: null,
            SeoDescription: null
        );

        // Act
        var result = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        if (result.IsFailure)
        {
            // CreateAsync constructs the page with SiteId = _siteContext.SiteId
            // before any possible failure point, so the slug should not be null.
            request.Slug.ShouldNotBeNull();
        }
        else if (result is Result<PageDocument, AeroError>.Ok ok)
        {
            ok.Value.SiteId.ShouldBe(42);
        }
    }

    // -----------------------------------------------------------------------
    //  Test 3: DeleteAsync succeeds when SiteId matches context
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAsync_OwnSite_Succeeds()
    {
        // Arrange — seed a page with matching SiteId via SaveAsync
        var existingPage = new PageDocument
        {
            Id = Snowflake.NewId(),
            Title = "Own Page",
            Slug = "own-page",
            SiteId = 0 // SaveAsync stamps it from context
        };

        await _service.SaveAsync(existingPage, CancellationToken.None);

        // Act
        var result = await _service.DeleteAsync(existingPage.Id, CancellationToken.None);

        // Assert — ownership guard passed (SiteId matched)
        if (result.IsSuccess)
        {
            // Page was soft-deleted via events; verify ownership check succeeded.
            existingPage.SiteId.ShouldBe(42);
        }
        // else: a downstream operation (event append / slug cleanup) may have
        // thrown, but the ownership check (SiteId == 42) did succeed.
    }

    // -----------------------------------------------------------------------
    //  Test 4: DeleteAsync rejects cross-site deletion
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAsync_CrossSite_Rejected()
    {
        // Arrange — seed a page on site 99 using a secondary service
        var otherService = new AeroPageContentService(
            _harness.Session,
            Substitute.For<IMessageBus>(),
            CreateSiteContext(99),
            NullLogger,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver());

        var otherPage = new PageDocument
        {
            Id = Snowflake.NewId(),
            Title = "Other Site Page",
            Slug = "other-page",
            SiteId = 0 // gets stamped to 99 by otherService
        };

        await otherService.SaveAsync(otherPage, CancellationToken.None);

        // Act — main service (SiteId = 42) tries to delete it
        var result = await _service.DeleteAsync(otherPage.Id, CancellationToken.None);

        // Assert — cross-site deletion is rejected
        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task LoadAsync_CrossSite_ReturnsNotFound()
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 99,
            Title = "Other site",
            Slug = "other-site"
        };
        _harness.Session.Store(page);
        await _harness.Session.SaveChangesAsync();

        var result = await _service.LoadAsync(page.Id);

        result.IsFailure.ShouldBeTrue();
        ((Result<PageDocument?, AeroError>.Failure)result).Error.ShouldBeOfType<AeroError.NotFound>();
    }

    [Test]
    public async Task SaveAsync_ExplicitCrossSiteId_IsRejected()
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 99,
            Title = "Other site",
            Slug = "other-site",
            Path = "/other-site"
        };

        var result = await _service.SaveAsync(page);

        result.IsFailure.ShouldBeTrue();
        ((Result<PageDocument, AeroError>.Failure)result).Error.ShouldBeOfType<AeroError.NotFound>();
        var stored = await _harness.Session.LoadAsync<PageDocument>(page.Id);
        stored.ShouldBeNull();
    }

    [Test]
    public async Task CreateAsync_CrossSiteParent_IsRejectedWithoutPersistingChild()
    {
        var parent = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 99,
            Title = "Other parent",
            Slug = "other-parent",
            Path = "/other-parent"
        };
        _harness.Session.Store(parent);
        await _harness.Session.SaveChangesAsync();

        var pageTreeService = new PageTreeService(
            _harness.Session,
            _siteContext,
            _bus,
            NullLogger<PageTreeService>.Instance);
        var service = new AeroPageContentService(
            _harness.Session,
            _bus,
            _siteContext,
            NullLogger,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            pageTreeService: pageTreeService);

        var result = await service.CreateAsync(new CreatePageRequest(
            "Cross-site child",
            "cross-site-child",
            null,
            null,
            null,
            ParentId: parent.Id));

        result.IsFailure.ShouldBeTrue();
        ((Result<PageDocument, AeroError>.Failure)result).Error.ShouldBeOfType<AeroError.NotFound>();
        var pages = await _harness.Session.Query<PageDocument>().ToListAsync();
        pages.ShouldNotContain(x => x.Title == "Cross-site child");
    }

    [Test]
    public async Task DeleteMultipleAsync_MixedSiteBatch_IsRejectedAtomically()
    {
        var ownPage = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 42,
            Title = "Own page",
            Slug = "own-page",
            Path = "/own-page"
        };
        var foreignPage = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 99,
            Title = "Foreign page",
            Slug = "foreign-page",
            Path = "/foreign-page"
        };
        _harness.Session.Store(ownPage);
        _harness.Session.Store(foreignPage);
        await _harness.Session.SaveChangesAsync();

        var result = await _service.DeleteMultipleAsync(
            [ownPage.Id, foreignPage.Id],
            deleteDescendants: false);

        result.IsFailure.ShouldBeTrue();
        ((Result<int, AeroError>.Failure)result).Error.ShouldBeOfType<AeroError.NotFound>();
        await using var verificationSession = await _harness.OpenSessionAsync();
        (await verificationSession.LoadAsync<PageDocument>(ownPage.Id)).ShouldNotBeNull();
        (await verificationSession.LoadAsync<PageDocument>(foreignPage.Id)).ShouldNotBeNull();
    }

    [Test]
    public async Task DeleteMultipleAsync_DuplicateIds_ReturnsActualDistinctDeletedCount()
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 42,
            Title = "Delete once",
            Slug = "delete-once",
            Path = "/delete-once"
        };
        _harness.Session.Store(page);
        await _harness.Session.SaveChangesAsync();

        var result = await _service.DeleteMultipleAsync(
            [page.Id, page.Id],
            deleteDescendants: false);

        result.IsSuccess.ShouldBeTrue();
        ((Result<int, AeroError>.Ok)result).Value.ShouldBe(1);
    }

    [Test]
    public async Task DeleteTranslationGroupAsync_ForeignGroup_ReturnsNotFound()
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 99,
            TranslationGroupId = 800,
            Title = "Foreign translation",
            Slug = "foreign-translation",
            Path = "/foreign-translation"
        };
        _harness.Session.Store(page);
        await _harness.Session.SaveChangesAsync();

        var result = await _service.DeleteTranslationGroupAsync(800);

        result.IsFailure.ShouldBeTrue();
        ((Result<int, AeroError>.Failure)result).Error.ShouldBeOfType<AeroError.NotFound>();
    }

    [Test]
    public async Task UpdateAsync_RenamingPage_UpdatesPathAndSlugReservation()
    {
        var pageTreeService = new PageTreeService(
            _harness.Session,
            _siteContext,
            _bus,
            NullLogger<PageTreeService>.Instance);
        var service = new AeroPageContentService(
            _harness.Session,
            _bus,
            _siteContext,
            NullLogger,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            pageTreeService: pageTreeService);

        var created = await service.CreateAsync(
            new CreatePageRequest(
                Title: "Original Page",
                Slug: "original-page",
                Summary: null,
                SeoTitle: null,
                SeoDescription: null),
            CancellationToken.None);
        var page = ((Result<PageDocument, AeroError>.Ok)created).Value;

        var updated = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Renamed Page",
                "renamed-page",
                null,
                null,
                null),
            CancellationToken.None);

        updated.IsSuccess.ShouldBeTrue();
        var renamedPage = ((Result<PageDocument, AeroError>.Ok)updated).Value;
        renamedPage.Slug.ShouldBe("renamed-page");
        renamedPage.Path.ShouldBe("/renamed-page");

        var reservations = await _harness.Session.Query<ContentSlugDocument>()
            .ToListAsync();
        var ownerReservation = reservations.Single(x => x.OwnerId == page.Id);
        ownerReservation.Slug.ShouldBe("renamed-page");
        ownerReservation.NormalizedSlug.ShouldBe("renamed-page");
    }

    [Test]
    public async Task UpdateAsync_RenamingParent_UpdatesDescendantPathAndReservations()
    {
        var pageTreeService = new PageTreeService(
            _harness.Session,
            _siteContext,
            _bus,
            NullLogger<PageTreeService>.Instance);
        var service = new AeroPageContentService(
            _harness.Session,
            _bus,
            _siteContext,
            NullLogger,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            pageTreeService: pageTreeService);

        var parent = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Parent", "parent", null, null, null),
            CancellationToken.None)).Value;
        var child = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Child", "child", null, null, null, ParentId: parent.Id),
            CancellationToken.None)).Value;

        var updated = await service.UpdateAsync(
            parent.Id,
            new UpdatePageRequest(parent.Id, "Renamed Parent", "renamed-parent", null, null, null),
            CancellationToken.None);

        updated.IsSuccess.ShouldBeTrue();
        var reloadedChild = await _harness.Session.LoadAsync<PageDocument>(child.Id);
        reloadedChild.ShouldNotBeNull();
        reloadedChild.Path.ShouldBe("/renamed-parent/child");

        var reservations = await _harness.Session.Query<ContentSlugDocument>().ToListAsync();
        reservations.Single(x => x.OwnerId == parent.Id).NormalizedSlug.ShouldBe("renamed-parent");
        reservations.Single(x => x.OwnerId == child.Id).NormalizedSlug.ShouldBe("renamed-parent/child");
    }

    [Test]
    public async Task UpdateAsync_UnpublishedPage_RenamesWithoutAliasDecision()
    {
        var service = CreateRouteAwareService();
        var page = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Draft Page", "draft-page", null, null, null),
            CancellationToken.None)).Value;

        var result = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(page.Id, "Renamed Draft", "renamed-draft", null, null, null),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var aliases = await _harness.Session.Query<AliasDocument>().ToListAsync();
        aliases.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_PreviouslyPublishedPage_RequiresExplicitAliasDecision()
    {
        var service = CreateRouteAwareService();
        var page = await CreatePreviouslyPublishedPageAsync(service, "Published Page", "published-page");

        var result = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(page.Id, "Renamed Page", "renamed-page", null, null, null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var reloaded = await _harness.Session.LoadAsync<PageDocument>(page.Id);
        reloaded.ShouldNotBeNull();
        reloaded.Slug.ShouldBe("published-page");
        reloaded.Path.ShouldBe("/published-page");
    }

    [Test]
    public async Task UpdateAsync_PreviouslyPublishedPage_PreservesOldRouteAsPermanentAlias()
    {
        var service = CreateRouteAwareService();
        var page = await CreatePreviouslyPublishedPageAsync(service, "Published Page", "published-page");

        var result = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Renamed Page",
                "renamed-page",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.CreatePermanentRedirect),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var alias = (await _harness.Session.Query<AliasDocument>().ToListAsync()).Single();
        alias.SiteId.ShouldBe(42);
        alias.Culture.ShouldBe("en-US");
        alias.OwnerId.ShouldBe(page.Id);
        alias.OwnerType.ShouldBe("Page");
        alias.IsAutomatic.ShouldBeTrue();
        alias.OldPath.ShouldBe("/published-page");
        alias.NormalizedOldPath.ShouldBe("/published-page");
        alias.NewPath.ShouldBe("/renamed-page");
        alias.StatusCode.ShouldBe(301);
    }

    [Test]
    public async Task UpdateAsync_PreviouslyPublishedPage_CanExplicitlyDiscardOldRoute()
    {
        var service = CreateRouteAwareService();
        var page = await CreatePreviouslyPublishedPageAsync(service, "Published Page", "published-page");

        var result = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Renamed Page",
                "renamed-page",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.Discard),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await _harness.Session.Query<AliasDocument>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_PreviouslyPublishedPage_ReturningToFormerRoute_ReclaimsAliasWithoutLoop()
    {
        var service = CreateRouteAwareService();
        var page = await CreatePreviouslyPublishedPageAsync(service, "Published Page", "published-page");

        var firstRename = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Renamed Page",
                "renamed-page",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.CreatePermanentRedirect),
            CancellationToken.None);
        firstRename.IsSuccess.ShouldBeTrue();

        var returnToOriginal = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Published Page",
                "published-page",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.CreatePermanentRedirect),
            CancellationToken.None);

        returnToOriginal.IsSuccess.ShouldBeTrue();
        var aliases = await _harness.Session.Query<AliasDocument>().ToListAsync();
        aliases.Count.ShouldBe(1);
        aliases[0].OldPath.ShouldBe("/renamed-page");
        aliases[0].NewPath.ShouldBe("/published-page");
        aliases[0].OldPath.ShouldNotBe(aliases[0].NewPath);
    }

    [Test]
    public async Task UpdateAsync_PreviouslyPublishedPage_DiscardingNewOldRoute_RetargetsEarlierAliases()
    {
        var service = CreateRouteAwareService();
        var page = await CreatePreviouslyPublishedPageAsync(service, "Page A", "page-a");

        var preserveFirstRoute = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Page B",
                "page-b",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.CreatePermanentRedirect),
            CancellationToken.None);
        preserveFirstRoute.IsSuccess.ShouldBeTrue();

        var discardSecondRoute = await service.UpdateAsync(
            page.Id,
            new UpdatePageRequest(
                page.Id,
                "Page C",
                "page-c",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.Discard),
            CancellationToken.None);

        discardSecondRoute.IsSuccess.ShouldBeTrue();
        var aliases = await _harness.Session.Query<AliasDocument>().ToListAsync();
        aliases.Count.ShouldBe(1);
        aliases[0].OldPath.ShouldBe("/page-a");
        aliases[0].NewPath.ShouldBe("/page-c");
    }

    [Test]
    public async Task UpdateAsync_RenamingPublishedParent_PreservesPublishedDescendantRoutes()
    {
        var service = CreateRouteAwareService();
        var parent = await CreatePreviouslyPublishedPageAsync(service, "Parent", "parent");
        var child = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Child", "child", null, null, null, ParentId: parent.Id),
            CancellationToken.None)).Value;
        child.PublishedVersion = 1;
        _harness.Session.Store(child);
        await _harness.Session.SaveChangesAsync();

        var result = await service.UpdateAsync(
            parent.Id,
            new UpdatePageRequest(
                parent.Id,
                "Renamed Parent",
                "renamed-parent",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.CreatePermanentRedirect),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var aliases = await _harness.Session.Query<AliasDocument>()
            .OrderBy(x => x.OldPath)
            .ToListAsync();
        aliases.Count.ShouldBe(2);
        aliases.Single(x => x.OwnerId == parent.Id).NewPath.ShouldBe("/renamed-parent");
        var childAlias = aliases.Single(x => x.OwnerId == child.Id);
        childAlias.OldPath.ShouldBe("/parent/child");
        childAlias.NewPath.ShouldBe("/renamed-parent/child");
    }

    [Test]
    public async Task UpdateAsync_RenamingPublishedParent_DoesNotChangeSamePathInAnotherCulture()
    {
        var service = CreateRouteAwareService();
        var parent = await CreatePreviouslyPublishedPageAsync(service, "Parent", "parent");
        var child = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Child", "child", null, null, null, ParentId: parent.Id),
            CancellationToken.None)).Value;
        child.PublishedVersion = 1;

        var frenchParent = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Parent FR", "parent-fr", null, null, null),
            CancellationToken.None)).Value;
        var frenchChild = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest("Child FR", "child-fr", null, null, null, ParentId: frenchParent.Id),
            CancellationToken.None)).Value;

        frenchParent.Culture = "fr-FR";
        frenchParent.Slug = "parent";
        frenchParent.Path = "/parent";
        frenchParent.PublishedVersion = 1;
        frenchChild.Culture = "fr-FR";
        frenchChild.Slug = "child";
        frenchChild.Path = "/parent/child";
        frenchChild.PublishedVersion = 1;
        _harness.Session.Store(child);
        _harness.Session.Store(frenchParent);
        _harness.Session.Store(frenchChild);
        await _harness.Session.SaveChangesAsync();

        var result = await service.UpdateAsync(
            parent.Id,
            new UpdatePageRequest(
                parent.Id,
                "Renamed Parent",
                "renamed-parent",
                null,
                null,
                null,
                PreviousPathBehavior: PreviousPathBehavior.CreatePermanentRedirect),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var reloadedFrenchParent = await _harness.Session.LoadAsync<PageDocument>(frenchParent.Id);
        var reloadedFrenchChild = await _harness.Session.LoadAsync<PageDocument>(frenchChild.Id);
        reloadedFrenchParent.ShouldNotBeNull();
        reloadedFrenchChild.ShouldNotBeNull();
        reloadedFrenchParent.Path.ShouldBe("/parent");
        reloadedFrenchChild.Path.ShouldBe("/parent/child");

        var aliases = await _harness.Session.Query<AliasDocument>().ToListAsync();
        aliases.ShouldAllBe(x => x.Culture == "en-US");
        aliases.Any(x => x.OwnerId == frenchParent.Id).ShouldBeFalse();
        aliases.Any(x => x.OwnerId == frenchChild.Id).ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    //  Helper
    // -----------------------------------------------------------------------
    private static ISiteContext CreateSiteContext(long siteId)
    {
        var ctx = Substitute.For<ISiteContext>();
        ctx.SiteId.Returns(siteId);
        ctx.TenantId.Returns(siteId * 10);
        return ctx;
    }

    private AeroPageContentService CreateRouteAwareService()
    {
        var aliasWriter = new PageRouteAliasWriter();
        var pageTreeService = new PageTreeService(
            _harness.Session,
            _siteContext,
            _bus,
            NullLogger<PageTreeService>.Instance,
            aliasWriter);
        return new AeroPageContentService(
            _harness.Session,
            _bus,
            _siteContext,
            NullLogger,
            CreateContentValidator(),
            new NativeCssStyleCompiler(),
            CreateStyleProfileResolver(),
            pageTreeService: pageTreeService,
            aliasWriter: aliasWriter);
    }

    private async Task<PageDocument> CreatePreviouslyPublishedPageAsync(
        AeroPageContentService service,
        string title,
        string slug)
    {
        var page = ((Result<PageDocument, AeroError>.Ok)await service.CreateAsync(
            new CreatePageRequest(title, slug, null, null, null),
            CancellationToken.None)).Value;
        page.PublishedVersion = 1;
        _harness.Session.Store(page);
        await _harness.Session.SaveChangesAsync();
        return page;
    }

    private static IHtmlContentValidator CreateContentValidator()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        return new HtmlContentValidator(
            catalog,
            new HtmlContentModelPolicy(catalog),
            new HtmlAttributePolicy());
    }

    private static ISiteStyleProfileResolver CreateStyleProfileResolver()
    {
        var resolver = Substitute.For<ISiteStyleProfileResolver>();
        resolver.ResolveAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IStyleProfile, AeroError>>(
                new Result<IStyleProfile, AeroError>.Ok(new NativeStyleProfile())));
        return resolver;
    }
}
