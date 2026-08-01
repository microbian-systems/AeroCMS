using Aero.Cms.Modules.Pages;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Services;

public sealed class SlugRegistryTests
{
    private SableTestHarness _harness = null!;
    private IDocumentSession _session = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _harness = new SableTestHarness()
            .WithSchema<ContentSlugDocument>();
        await _harness.InitializeAsync();
        _session = _harness.Session;
    }

    [After(Test)]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    //  Test 1: ReserveAsync stamps SiteId on the stored reservation document
    // -----------------------------------------------------------------------
    [Test]
    public async Task ReserveAsync_StampsSiteId()
    {
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
        await _session.SaveChangesAsync();

        // Query for the stored document and assert
        var stored = await _session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId);

        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.SiteId).IsEqualTo(42);
        await Assert.That(stored.Culture).IsEqualTo("en-US");
    }

    [Test]
    public async Task ReserveAsync_AllowsSameSlugAcrossDifferentSites()
    {
        // Seed a reservation for siteId=1
        var seeded = ContentSlugDocument.Create("/", 100, ContentSlugOwnerType.Page, siteId: 1);
        _session.Store(seeded);
        await _session.SaveChangesAsync();

        // Act — reserve the same slug for a different site
        await ContentSlugReservation.ReserveAsync(
            _session,
            ownerId: 200,
            ContentSlugOwnerType.Page,
            slug: "/",
            siteId: 2,
            previousSlug: null,
            CancellationToken.None);
        await _session.SaveChangesAsync();

        // Assert — the stored document should have siteId=2 and ownerId=200
        var stored = await _session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == 2);

        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.SiteId).IsEqualTo(2);
        await Assert.That(stored.OwnerId).IsEqualTo(200);
        await Assert.That(stored.NormalizedSlug).IsEqualTo(ContentSlugDocument.Normalize("/"));
    }

    [Test]
    public async Task ReserveAsync_AllowsSameSlugAcrossDifferentCultures()
    {
        // Seed a reservation in "en-US"
        var seeded = ContentSlugDocument.Create("/", 100, ContentSlugOwnerType.Page, siteId: 1, culture: "en-US");
        _session.Store(seeded);
        await _session.SaveChangesAsync();

        // Act — reserve the same slug+site for a different culture
        await ContentSlugReservation.ReserveAsync(
            _session,
            ownerId: 200,
            ContentSlugOwnerType.Page,
            slug: "/",
            siteId: 1,
            culture: "es-MX",
            previousSlug: null,
            CancellationToken.None);
        await _session.SaveChangesAsync();

        // Assert — the stored document should have culture "es-MX" and ownerId=200
        var stored = await _session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x => x.Culture == "es-MX");

        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.SiteId).IsEqualTo(1);
        await Assert.That(stored.Culture).IsEqualTo("es-MX");
        await Assert.That(stored.OwnerId).IsEqualTo(200);
        await Assert.That(stored.NormalizedSlug).IsEqualTo(ContentSlugDocument.Normalize("/"));
    }
}
