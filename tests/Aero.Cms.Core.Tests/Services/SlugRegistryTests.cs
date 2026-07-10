using Aero.Cms.Modules.Pages;
using AeroDB;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Services;

public sealed class SlugRegistryTests
{
    private IDocumentSession _session = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _session = Substitute.For<IDocumentSession>();
    }

    [After(Test)]
    public async Task TearDown()
    {
        // No-op cleanup
    }

    // -----------------------------------------------------------------------
    //  Test 1: ReserveAsync stamps SiteId on the stored reservation document
    // -----------------------------------------------------------------------
    [Test]
    public async Task ReserveAsync_StampsSiteId()
    {
        // Arrange — mock an empty query result for session.Query<ContentSlugDocument>().
        // ReserveAsync internally calls FirstOrDefaultAsync on the queryable, which
        // uses Provider/Expression.  We substitute IQueryable<> so LINQ operations
        // delegate to the in-memory list provider (simulating "no existing reservation").
        var docs = new List<ContentSlugDocument>().AsQueryable();
        var queryable = Substitute.For<IQueryable<ContentSlugDocument>>();
        queryable.Provider.Returns(docs.Provider);
        queryable.Expression.Returns(docs.Expression);
        queryable.ElementType.Returns(docs.ElementType);
        queryable.GetEnumerator().Returns(docs.GetEnumerator());

        // session.Query<T>() returns IMartenQueryable<T>; we return IQueryable<T> which
        // satisfies assignability for the mock.
        _session.Query<ContentSlugDocument>().Returns(queryable);

        const long ownerId = 1;
        const string slug = "my-doc";
        const long siteId = 42;

        // Act
        await ContentSlugReservation.ReserveAsync(
            _session,
            ownerId,
            ContentSlugOwnerType.Page,
            slug,
            siteId,
            previousSlug: null,
            CancellationToken.None
        );

        // Assert — the stored ContentSlugDocument should have SiteId == 42
        _session.Received(1).Store(
            Arg.Is<ContentSlugDocument>(d => d.SiteId == 42 && d.Culture == "en-US")
        );
    }

    [Test]
    public async Task ReserveAsync_AllowsSameSlugAcrossDifferentSites()
    {
        var docs = new List<ContentSlugDocument>
        {
            ContentSlugDocument.Create("/", 100, ContentSlugOwnerType.Page, siteId: 1)
        }.AsQueryable();

        var queryable = Substitute.For<IQueryable<ContentSlugDocument>>();
        queryable.Provider.Returns(docs.Provider);
        queryable.Expression.Returns(docs.Expression);
        queryable.ElementType.Returns(docs.ElementType);
        queryable.GetEnumerator().Returns(docs.GetEnumerator());

        _session.Query<ContentSlugDocument>().Returns(queryable);

        await ContentSlugReservation.ReserveAsync(
            _session,
            ownerId: 200,
            ContentSlugOwnerType.Page,
            slug: "/",
            siteId: 2,
            previousSlug: null,
            CancellationToken.None);

        _session.Received(1).Store(
            Arg.Is<ContentSlugDocument>(d =>
                d.SiteId == 2 &&
                d.OwnerId == 200 &&
                d.NormalizedSlug == ContentSlugDocument.Normalize("/")));
    }

    [Test]
    public async Task ReserveAsync_AllowsSameSlugAcrossDifferentCultures()
    {
        var docs = new List<ContentSlugDocument>
        {
            ContentSlugDocument.Create("/", 100, ContentSlugOwnerType.Page, siteId: 1, culture: "en-US")
        }.AsQueryable();

        var queryable = Substitute.For<IQueryable<ContentSlugDocument>>();
        queryable.Provider.Returns(docs.Provider);
        queryable.Expression.Returns(docs.Expression);
        queryable.ElementType.Returns(docs.ElementType);
        queryable.GetEnumerator().Returns(docs.GetEnumerator());

        _session.Query<ContentSlugDocument>().Returns(queryable);

        await ContentSlugReservation.ReserveAsync(
            _session,
            ownerId: 200,
            ContentSlugOwnerType.Page,
            slug: "/",
            siteId: 1,
            culture: "es-MX",
            previousSlug: null,
            CancellationToken.None);

        _session.Received(1).Store(
            Arg.Is<ContentSlugDocument>(d =>
                d.SiteId == 1 &&
                d.Culture == "es-MX" &&
                d.OwnerId == 200 &&
                d.NormalizedSlug == ContentSlugDocument.Normalize("/")));
    }
}
