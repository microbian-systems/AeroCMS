using Aero.Cms.Core.Models;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class MediaAssetSablePersistenceTests
{
    [Test]
    public async Task Strict_media_asset_round_trips_embedded_attribution()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<MediaAsset>(SchemaMode.Strict);
        await harness.InitializeAsync();

        harness.Session.Store(new MediaAsset
        {
            Id = 73_001,
            SiteId = 42,
            FileName = "hero.jpg",
            Url = "/media/hero.jpg",
            MimeType = "image/jpeg",
            FileSize = 12_345,
            Width = 1_920,
            Height = 1_080,
            Attribution = new MediaAttribution
            {
                CreatorName = "Ada Lovelace",
                CreatorUrl = "https://example.test/ada",
                SourceUrl = "https://example.test/hero",
                Platform = "Pexels",
                MediaType = MediaType.Image
            }
        });

        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync(
            new SessionOptions { Tracking = DocumentTracking.None });
        var saved = (await verificationSession.Query<MediaAsset>().ToListAsync())
            .SingleOrDefault(asset => asset.FileName == "hero.jpg");

        saved.ShouldNotBeNull();
        saved.Attribution.ShouldNotBeNull();
        saved.Attribution.CreatorName.ShouldBe("Ada Lovelace");
        saved.Attribution.Platform.ShouldBe("Pexels");
    }
}
