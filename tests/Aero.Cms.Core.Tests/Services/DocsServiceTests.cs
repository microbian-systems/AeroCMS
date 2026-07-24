using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class DocsServiceTests
{
    private SableTestHarness _harness = null!;
    private IDocumentSession _session = null!;
    private IMessageBus _bus = null!;
    private ISiteContext _siteContext = null!;
    private ILogger<DocsContentService> _logger = null!;
    private DocsContentService _service = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _harness = new SableTestHarness()
            .WithSchema<DocsPage>()
            .WithSchema<ContentSlugDocument>();
        await _harness.InitializeAsync();
        _session = _harness.Session;

        _bus = Substitute.For<IMessageBus>();
        _siteContext = Substitute.For<ISiteContext>();
        _logger = Substitute.For<ILogger<DocsContentService>>();

        _siteContext.SiteId.Returns(42);

        _service = new DocsContentService(_session, _bus, _siteContext, _logger);
    }

    [After(Test)]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    //  Test 1: SaveAsync stamps SiteId from context onto the entity
    // -----------------------------------------------------------------------
    [Test]
    public async Task SaveAsync_StampsSiteId_FromContext()
    {
        // Use a fresh session for isolation (avoids any in-memory state from other tests)
        await using var freshSession = await _harness.OpenSessionAsync();
        var page = new DocsPage
        {
            Id = Snowflake.NewId(),
            Title = "Test Doc",
            Slug = "test-doc",
            PublicationState = ContentPublicationState.Published,
            CreatedBy = "system",
            ModifiedBy = "system"
        };
        var service = new DocsContentService(
            freshSession, Substitute.For<IMessageBus>(), CreateSiteContext(42), _logger);

        var result = await service.SaveAsync(page, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var stored = await freshSession.LoadAsync<DocsPage>(page.Id);
        stored.ShouldNotBeNull();
        stored.SiteId.ShouldBe(42);
    }

    // -----------------------------------------------------------------------
    //  Test 2: ToViewModel preserves SiteId through the event pipeline
    // -----------------------------------------------------------------------
    [Test]
    public async Task ToViewModel_MapsSiteId()
    {
        // Arrange
        var pageId = Snowflake.NewId();
        var page = new DocsPage
        {
            Id = pageId,
            Title = "Test Doc",
            Slug = "test-doc",
            SiteId = 0,
            PublicationState = ContentPublicationState.Published,
            CreatedBy = "system",
            ModifiedBy = "system"
        };

        // Capture the message that gets published via bus
        DocViewModelCreated? publishedMessage = null;
        _bus.When(x => x.PublishAsync(Arg.Any<DocViewModelCreated>()))
            .Do(callInfo => { publishedMessage = callInfo.Arg<DocViewModelCreated>(); });

        // Act — real session has no data, LoadAsync returns null => treated as new page
        await _service.SaveAsync(page, CancellationToken.None);

        // Assert — the event published via bus contains a DocViewModel with SiteId=42,
        // proving that the private ToViewModel method correctly mapped the property.
        publishedMessage.ShouldNotBeNull();
        publishedMessage.doc.SiteId.ShouldBe(42);
    }

    // -----------------------------------------------------------------------
    //  Test 3: DeleteAsync succeeds when SiteId matches context
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAsync_OwnSite_Succeeds()
    {
        // Arrange — seed a DocsPage in the real session
        const long pageId = 100;
        var existingPage = new DocsPage
        {
            Id = pageId,
            Title = "Own Doc",
            Slug = "own-doc",
            SiteId = 42, // matches context
            PublicationState = ContentPublicationState.Published,
            CreatedBy = "system",
            ModifiedBy = "system"
        };
        _session.Store(existingPage);
        await _session.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(pageId, CancellationToken.None);

        // Assert — page should be deleted from DB
        result.IsSuccess.ShouldBeTrue();
        var stored = await _session.LoadAsync<DocsPage>(pageId);
        stored.ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    //  Test 4: DeleteAsync rejects cross-site deletion
    // -----------------------------------------------------------------------
    [Test]
    public async Task DeleteAsync_CrossSite_Rejected()
    {
        // Arrange — seed a DocsPage with a different SiteId
        const long pageId = 200;
        var existingPage = new DocsPage
        {
            Id = pageId,
            Title = "Other Site Doc",
            Slug = "other-doc",
            SiteId = 99, // different site
            PublicationState = ContentPublicationState.Published,
            CreatedBy = "system",
            ModifiedBy = "system"
        };
        _session.Store(existingPage);
        await _session.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(pageId, CancellationToken.None);

        // Assert — deletion should be rejected
        result.IsFailure.ShouldBeTrue();

        // Page should still exist in DB
        var stored = await _session.LoadAsync<DocsPage>(pageId);
        stored.ShouldNotBeNull();
        stored.SiteId.ShouldBe(99);
    }

    [Test]
    public async Task DeleteAsync_OwnSiteParent_IsRejectedWithoutMutationOrEvents()
    {
        const long parentId = 210;
        const long childId = 211;
        var parent = new DocsPage
        {
            Id = parentId,
            Title = "Parent",
            Slug = "docs/parent",
            SiteId = 42,
            PublicationState = ContentPublicationState.Published
        };
        var child = new DocsPage
        {
            Id = childId,
            ParentId = parentId,
            Title = "Child",
            Slug = "docs/parent/child",
            SiteId = 42,
            PublicationState = ContentPublicationState.Published
        };
        _session.Store(parent, child);
        await _session.SaveChangesAsync();

        var result = await _service.DeleteAsync(parentId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        (await _session.LoadAsync<DocsPage>(parentId)).ShouldNotBeNull();
        (await _session.LoadAsync<DocsPage>(childId)).ShouldNotBeNull();
        await _bus.DidNotReceive().PublishAsync(Arg.Any<DocViewModelDeleted>());
        await _bus.DidNotReceive().PublishAsync(Arg.Any<DocsPageContentUpdatedEvent>());
    }

    [Test]
    public async Task DeleteAsync_OwnSiteLeaf_WithForeignChildIdReference_Succeeds()
    {
        const long pageId = 220;
        _session.Store(
            new DocsPage
            {
                Id = pageId,
                Title = "Local leaf",
                Slug = "docs/local-leaf",
                SiteId = 42,
                PublicationState = ContentPublicationState.Published
            },
            new DocsPage
            {
                Id = 221,
                ParentId = pageId,
                Title = "Foreign child",
                Slug = "docs/foreign-child",
                SiteId = 99,
                PublicationState = ContentPublicationState.Published
            });
        await _session.SaveChangesAsync();

        var result = await _service.DeleteAsync(pageId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await _session.LoadAsync<DocsPage>(pageId)).ShouldBeNull();
        (await _session.LoadAsync<DocsPage>(221)).ShouldNotBeNull();
    }

    [Test]
    public async Task GetPublishedAsync_ReturnsSeededHierarchyForSiteAndCulture()
    {
        var root = new DocsPage
        {
            Id = 301,
            SiteId = 42,
            Culture = "en-US",
            Title = "Documentation",
            Slug = "docs",
            PublicationState = ContentPublicationState.Published,
            Order = 0
        };
        var chapter = new DocsPage
        {
            Id = 302,
            SiteId = 42,
            Culture = "en-US",
            ParentId = root.Id,
            Title = "Getting Started",
            Slug = "docs/getting-started",
            PublicationState = ContentPublicationState.Published,
            Order = 0
        };
        _session.Store(root, chapter);
        await _session.SaveChangesAsync();

        var result = await _service.GetPublishedAsync("en-US", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var pages = ((global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok)result).Value;
        pages.Select(page => page.Slug).ShouldBe(["docs", "docs/getting-started"]);
    }

    [Test]
    public async Task SaveFromViewModelAsync_ForeignExistingId_IsRejectedWithoutRehoming()
    {
        var foreign = new DocsPage { Id = 401, SiteId = 99, Title = "Foreign", Slug = "foreign" };
        _session.Store(foreign);
        await _session.SaveChangesAsync();

        var result = await _service.SaveFromViewModelAsync(new DocViewModel
        {
            Id = foreign.Id, SiteId = 42, Title = "Attacker title", Slug = "attacker-slug"
        });

        result.IsFailure.ShouldBeTrue();
        var stored = await _session.LoadAsync<DocsPage>(foreign.Id);
        stored!.SiteId.ShouldBe(99);
        stored.Title.ShouldBe("Foreign");
    }

    [Test]
    public async Task SaveAsync_RejectsForeignParentAndTranslationGroup()
    {
        var foreign = new DocsPage { Id = 501, SiteId = 99, TranslationGroupId = 501, Title = "Foreign", Slug = "foreign" };
        _session.Store(foreign);
        await _session.SaveChangesAsync();

        var result = await _service.SaveAsync(new DocsPage
        {
            Title = "Local", Slug = "local", ParentId = foreign.Id, TranslationGroupId = foreign.Id
        });

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task GetByIdsAsync_OnlyReturnsCurrentSitePages()
    {
        _session.Store(
            new DocsPage { Id = 601, SiteId = 42, Title = "Local", Slug = "local" },
            new DocsPage { Id = 602, SiteId = 99, Title = "Foreign", Slug = "foreign" });
        await _session.SaveChangesAsync();

        var result = await _service.GetByIdsAsync([601, 602]);
        var pages = ((global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>.Ok)result).Value;
        pages.Select(x => x.Id).ShouldBe([601]);
    }

    private static ISiteContext CreateSiteContext(long siteId)
    {
        var ctx = Substitute.For<ISiteContext>();
        ctx.SiteId.Returns(siteId);
        ctx.TenantId.Returns(siteId * 10);
        return ctx;
    }
}
