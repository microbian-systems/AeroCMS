using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
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
    private IBlockService _blockService = null!;
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
        _blockService = Substitute.For<IBlockService>();
        _logger = Substitute.For<ILogger<DocsContentService>>();

        _siteContext.SiteId.Returns(42);

        _service = new DocsContentService(_session, _blockService, _bus, _siteContext, _logger);
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
            freshSession, _blockService, Substitute.For<IMessageBus>(), CreateSiteContext(42), _logger);

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
        object? publishedMessage = null;
        _bus.When(x => x.PublishAsync(Arg.Any<object>()))
            .Do(callInfo => { publishedMessage = callInfo.Arg<object>(); });

        // Act — real session has no data, LoadAsync returns null => treated as new page
        await _service.SaveAsync(page, CancellationToken.None);

        // Assert — the event published via bus contains a DocViewModel with SiteId=42,
        // proving that the private ToViewModel method correctly mapped the property.
        publishedMessage.ShouldNotBeNull();
        publishedMessage.ShouldBeOfType<DocViewModelCreated>();
        var docCreated = (DocViewModelCreated)publishedMessage!;
        docCreated.doc.SiteId.ShouldBe(42);
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

    private static ISiteContext CreateSiteContext(long siteId)
    {
        var ctx = Substitute.For<ISiteContext>();
        ctx.SiteId.Returns(siteId);
        ctx.TenantId.Returns(siteId * 10);
        return ctx;
    }
}
