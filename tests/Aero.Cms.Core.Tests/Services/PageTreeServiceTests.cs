using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Marten;
using Microsoft.Extensions.Logging;
using MysticMind.PostgresEmbed;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PageTreeServiceTests
{
    private static PgServer? s_pgServer;
    private static IDocumentStore s_store = null!;
    private static readonly SemaphoreSlim s_initLock = new(1, 1);
    private static bool s_initialized;

    private const long SiteId = 42;

    // ── Test page IDs ──────────────────────────────────────────────────
    // Tree: /about(2)  /home(1)  /tutorials(3) → /tutorials/dotnet(4)  /blog(5, hidden)
    private const long PageRoot = 1;
    private const long PageAbout = 2;
    private const long PageTutorials = 3;
    private const long PageDotnet = 4;
    private const long PageBlog = 5;

    private static IMessageBus s_bus = Substitute.For<IMessageBus>();
    private static ISiteContext s_siteContext = null!;

    static PageTreeServiceTests()
    {
        s_siteContext = Substitute.For<ISiteContext>();
        s_siteContext.SiteId.Returns(SiteId);
        s_siteContext.TenantId.Returns(SiteId * 10);
    }

    /// <summary>
    /// One-time lazy initializer called before first test. Starts embedded
    /// Postgres and creates the Marten document store. Uses a semaphore to
    /// ensure single initialization even with parallel tests.
    /// </summary>
    private static async Task EnsureInitializedAsync()
    {
        if (s_initialized) return;

        await s_initLock.WaitAsync();
        try
        {
            if (s_initialized) return;

            s_pgServer = new PgServer(
                "18.3.0",
                "aero",
                port: 5433,
                clearInstanceDirOnStop: true
            );

            await s_pgServer.StartAsync();

            var connectionString = $"Host=localhost;Port={s_pgServer.PgPort};Username=aero;Database=aero;";

            s_store = DocumentStore.For(opts =>
            {
                opts.Connection(connectionString);
                opts.DatabaseSchemaName = "public";
                opts.Schema.For<PageDocument>().DocumentAlias("pages");
                opts.Schema.For<PageDocument>().Identity(x => x.Id);
                opts.Schema.For<PageDocument>().Index(x => x.SiteId);
                opts.Schema.For<PageDocument>().UniqueIndex(x => x.SiteId, x => x.ParentId, x => x.Slug);
                opts.Schema.For<PageDocument>().Index(x => x.Path);
                opts.Schema.For<PageDocument>().Index(x => x.ParentId);
                opts.Schema.For<PageDocument>().SoftDeleted();
            });

            await s_store.Advanced.Clean.CompletelyRemoveAllAsync();
            s_initialized = true;
        }
        finally
        {
            s_initLock.Release();
        }
    }

    /// <summary>
    /// Run once when ALL tests in this class have finished.
    /// Stops Postgres and deletes data files.
    /// </summary>
    public static async Task CleanupAsync()
    {
        if (s_store is IDisposable d)
            d.Dispose();

        if (s_pgServer is not null)
        {
            await s_pgServer.StopAsync();
            await s_pgServer.DisposeAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────
    private static PageTreeService CreateService(IDocumentSession session)
    {
        var logger = Substitute.For<ILogger<PageTreeService>>();
        return new PageTreeService(session, s_siteContext, s_bus, logger);
    }

    private static async Task SeedAsync(IDocumentSession session)
    {
        session.Store(
            new PageDocument
            {
                Id = PageRoot, SiteId = SiteId, Title = "Home", Slug = "home",
                Path = "/home", Depth = 0, Order = 0,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = PageAbout, SiteId = SiteId, Title = "About", Slug = "about",
                Path = "/about", Depth = 0, Order = 0,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = PageTutorials, SiteId = SiteId, Title = "Tutorials", Slug = "tutorials",
                Path = "/tutorials", Depth = 0, Order = 1,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = PageDotnet, SiteId = SiteId, Title = ".NET Tutorials", Slug = "dotnet",
                Path = "/tutorials/dotnet", Depth = 1, ParentId = PageTutorials, Order = 0,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = PageBlog, SiteId = SiteId, Title = "Blog", Slug = "blog",
                Path = "/blog", Depth = 0, Order = 2, IsHidden = true,
                PublicationState = ContentPublicationState.Published
            }
        );

        await session.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 1: GetTreeAsync
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task GetTreeAsync_ReturnsAllPages_SortedByPath()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.GetTreeAsync();

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
        {
            var paths = ok.Value.Select(p => p.Path).ToList();
            await Assert.That(paths).IsEqualTo(
            [
                "/about",
                "/blog",
                "/home",
                "/tutorials",
                "/tutorials/dotnet"
            ]);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 2: GetChildrenAsync
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task GetChildrenAsync_RootLevel_ReturnsDirectChildrenOrdered()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.GetChildrenAsync(parentId: null);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
        {
            var titles = ok.Value.Select(p => p.Title).ToList();
            await Assert.That(titles).IsEqualTo(["About", "Home", "Tutorials", "Blog"]);
        }
    }

    [Test]
    public async Task GetChildrenAsync_UnderTutorials_ReturnsDotnet()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.GetChildrenAsync(parentId: PageTutorials);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Count).IsEqualTo(1);
            await Assert.That(ok.Value[0].Title).IsEqualTo(".NET Tutorials");
            await Assert.That(ok.Value[0].ParentId).IsEqualTo(PageTutorials);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 3: GetAncestorsAsync (single-query breadcrumb)
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task GetAncestorsAsync_DotnetPage_ReturnsTutorials()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.GetAncestorsAsync(PageDotnet);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Count).IsEqualTo(1);
            await Assert.That(ok.Value[0].Title).IsEqualTo("Tutorials");
        }
    }

    [Test]
    public async Task GetAncestorsAsync_RootPage_ReturnsEmpty()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.GetAncestorsAsync(PageTutorials);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetAncestorsAsync_NotFound_ReturnsError()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        var service = CreateService(session);
        var result = await service.GetAncestorsAsync(999);

        await Assert.That(result.IsFailure).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 4: ComputePathAsync
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task ComputePathAsync_RootLevel_ReturnsCorrectPath()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        var service = CreateService(session);
        var result = await service.ComputePathAsync(SiteId, null, "contact");

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<(string Path, int Depth), AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Path).IsEqualTo("/contact");
            await Assert.That(ok.Value.Depth).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ComputePathAsync_ChildLevel_ReturnsNestedPath()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.ComputePathAsync(SiteId, PageTutorials, "csharp");

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<(string Path, int Depth), AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Path).IsEqualTo("/tutorials/csharp");
            await Assert.That(ok.Value.Depth).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ComputePathAsync_DuplicateSlug_ReturnsConflict()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.ComputePathAsync(SiteId, PageTutorials, "dotnet");

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task ComputePathAsync_ParentNotFound_ReturnsError()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        var service = CreateService(session);
        var result = await service.ComputePathAsync(SiteId, 999, "orphan");

        await Assert.That(result.IsFailure).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 5: GetNextSiblingOrderAsync
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task GetNextSiblingOrderAsync_NoSiblings_ReturnsZero()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        var service = CreateService(session);
        var result = await service.GetNextSiblingOrderAsync(SiteId, null);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<int, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetNextSiblingOrderAsync_WithSiblings_ReturnsMaxPlusOne()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.GetNextSiblingOrderAsync(SiteId, null);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<int, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value).IsEqualTo(3);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 6: MoveAsync
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task MoveAsync_ValidMove_UpdatesPathAndDepth()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.MoveAsync(PageAbout, PageTutorials, order: 1);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<PageDocument, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Path).IsEqualTo("/tutorials/about");
            await Assert.That(ok.Value.Depth).IsEqualTo(1);
            await Assert.That(ok.Value.ParentId).IsEqualTo(PageTutorials);
        }
    }

    [Test]
    public async Task MoveAsync_MoveToRoot_ResetsDepth()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.MoveAsync(PageDotnet, null);

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<PageDocument, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value.Path).IsEqualTo("/dotnet");
            await Assert.That(ok.Value.Depth).IsEqualTo(0);
            await Assert.That(ok.Value.ParentId).IsNull();
        }
    }

    [Test]
    public async Task MoveAsync_CircularReference_ReturnsConflict()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.MoveAsync(PageTutorials, PageDotnet);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task MoveAsync_NotFound_ReturnsError()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        var service = CreateService(session);
        var result = await service.MoveAsync(999, null);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task MoveAsync_UpdatesDescendantPaths()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();

        session.Store(
            new PageDocument
            {
                Id = PageTutorials, SiteId = SiteId, Title = "Tutorials", Slug = "tutorials",
                Path = "/tutorials", Depth = 0, Order = 0,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = PageDotnet, SiteId = SiteId, Title = ".NET", Slug = "dotnet",
                Path = "/tutorials/dotnet", Depth = 1, ParentId = PageTutorials, Order = 0,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = 100, SiteId = SiteId, Title = "Advanced", Slug = "advanced",
                Path = "/tutorials/dotnet/advanced", Depth = 2, ParentId = PageDotnet, Order = 0,
                PublicationState = ContentPublicationState.Published
            },
            new PageDocument
            {
                Id = PageAbout, SiteId = SiteId, Title = "About", Slug = "about",
                Path = "/about", Depth = 0, Order = 0,
                PublicationState = ContentPublicationState.Published
            }
        );
        await session.SaveChangesAsync();

        var service = CreateService(session);
        var result = await service.MoveAsync(PageTutorials, PageAbout, order: 0);

        await Assert.That(result.IsSuccess).IsTrue();

        var dotnet = await session.LoadAsync<PageDocument>(PageDotnet);
        await Assert.That(dotnet!.Path).IsEqualTo("/about/tutorials/dotnet");
        await Assert.That(dotnet.Depth).IsEqualTo(2);

        var advanced = await session.LoadAsync<PageDocument>(100);
        await Assert.That(advanced!.Path).IsEqualTo("/about/tutorials/dotnet/advanced");
        await Assert.That(advanced.Depth).IsEqualTo(3);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Test 7: UpdateDescendantPathsAsync
    // ═══════════════════════════════════════════════════════════════════
    [Test]
    public async Task UpdateDescendantPathsAsync_UpdatesAllDescendants()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.UpdateDescendantPathsAsync(
            PageTutorials,
            oldPath: "/tutorials",
            newPath: "/learn");

        await Assert.That(result.IsSuccess).IsTrue();
        if (result is Result<bool, AeroError>.Ok ok)
        {
            await Assert.That(ok.Value).IsTrue();
        }

        var dotnet = await session.LoadAsync<PageDocument>(PageDotnet);
        await Assert.That(dotnet!.Path).IsEqualTo("/learn/dotnet");
        await Assert.That(dotnet.Depth).IsEqualTo(1);
    }

    [Test]
    public async Task UpdateDescendantPathsAsync_SamePath_NoOp()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        await SeedAsync(session);

        var service = CreateService(session);
        var result = await service.UpdateDescendantPathsAsync(
            PageTutorials,
            oldPath: "/tutorials",
            newPath: "/tutorials");

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task UpdateDescendantPathsAsync_NotFound_ReturnsError()
    {
        await EnsureInitializedAsync();
        await using var session = s_store.LightweightSession();
        var service = CreateService(session);
        var result = await service.UpdateDescendantPathsAsync(999, "/old", "/new");

        await Assert.That(result.IsFailure).IsTrue();
    }
}
