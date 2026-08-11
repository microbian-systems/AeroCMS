using Aero.Cms.Core.Models;
using Aero.Cms.Modules.Media;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using System.Linq.Expressions;

namespace Aero.Cms.Core.Tests.Services;

public sealed class MediaServiceWebRootTests
{
    [Test]
    public async Task SeedFromDirectoryAsync_ImportsFromContentRootWhenWebRootPathIsNull()
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"aero-media-content-root-{Guid.NewGuid():N}");
        var hydratedImages = Path.Combine(contentRoot, "wwwroot", "media", "hydrated-images");
        Directory.CreateDirectory(hydratedImages);

        try
        {
            var imagePath = Path.Combine(hydratedImages, "starter.webp");
            await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);

            var storedAssets = new List<MediaAsset>();
            var session = Substitute.For<IDocumentSession>();
            var emptyMediaQuery = Substitute.For<ISableQueryable<MediaAsset>>();
            emptyMediaQuery.FirstOrDefaultAsync(
                    Arg.Any<Expression<Func<MediaAsset, bool>>>(),
                    Arg.Any<CancellationToken>())
                .Returns((MediaAsset?)null);
            session.Query<MediaAsset>()
                .Returns(emptyMediaQuery);
            session.When(candidate => candidate.Store(Arg.Any<MediaAsset>()))
                .Do(call => storedAssets.Add(call.Arg<MediaAsset>()));

            var environment = Substitute.For<IWebHostEnvironment>();
            environment.WebRootPath.Returns((string?)null);
            environment.ContentRootPath.Returns(contentRoot);

            var service = new MediaService(session, environment);

            var result = await service.SeedFromDirectoryAsync("hydrated-images");

            if (result is Result<int, AeroError>.Failure failure)
            {
                throw new InvalidOperationException(failure.Error.ToString());
            }

            await Assert.That(result.IsSuccess).IsTrue();
            var success = (Result<int, AeroError>.Ok)result;
            await Assert.That(success.Value).IsEqualTo(1);
            await Assert.That(storedAssets).Count().IsEqualTo(1);
            await Assert.That(storedAssets[0].Url)
                .IsEqualTo("/media/hydrated-images/starter.webp");
            await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }
}
