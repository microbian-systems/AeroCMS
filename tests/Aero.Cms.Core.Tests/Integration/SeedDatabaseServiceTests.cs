using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Data;
using Aero.Cms.Modules.Media;
using Aero.Cms.Modules.Modules.Services;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Tenant;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class SeedDatabaseServiceTests
{
    [Test]
    public async Task StarterMediaStager_CopiesRclAssetsWithoutOverwritingHostMedia()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"aerocms-starter-media-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(testRoot, "source");
        var hostWebRoot = Path.Combine(testRoot, "host", "wwwroot");
        var sourceMedia = Path.Combine(sourceRoot, "_content", "Aero.Cms.UI", "media");
        Directory.CreateDirectory(Path.Combine(sourceMedia, "hydrated-images"));
        Directory.CreateDirectory(Path.Combine(hostWebRoot, "media"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceMedia, "data-center.png"), "rcl-data");
            await File.WriteAllTextAsync(Path.Combine(sourceMedia, "hydrated-images", "photo.jpg"), "photo-data");
            await File.WriteAllTextAsync(Path.Combine(hostWebRoot, "media", "data-center.png"), "host-data");

            var environment = Substitute.For<IWebHostEnvironment>();
            environment.WebRootPath.Returns(hostWebRoot);
            environment.WebRootFileProvider.Returns(new PhysicalFileProvider(sourceRoot));

            var staged = await StarterMediaStager.StageAsync(environment);

            staged.ShouldBeTrue();
            (await File.ReadAllTextAsync(Path.Combine(hostWebRoot, "media", "data-center.png"))).ShouldBe("host-data");
            (await File.ReadAllTextAsync(Path.Combine(hostWebRoot, "media", "hydrated-images", "photo.jpg"))).ShouldBe("photo-data");
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task StarterMediaStager_FailedCopyLeavesNoFinalFileAndCanBeRetried()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"aerocms-starter-media-retry-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(testRoot, "source");
        var hostWebRoot = Path.Combine(testRoot, "host", "wwwroot");
        var sourceMedia = Path.Combine(sourceRoot, "_content", "Aero.Cms.UI", "media");
        Directory.CreateDirectory(sourceMedia);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceMedia, "data-center.png"), "complete-rcl-data");

            var environment = Substitute.For<IWebHostEnvironment>();
            environment.WebRootPath.Returns(hostWebRoot);
            environment.WebRootFileProvider.Returns(new PhysicalFileProvider(sourceRoot));

            var failingStream = Substitute.For<Stream>();
            failingStream
                .CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    var target = call.ArgAt<Stream>(0);
                    await target.WriteAsync("partial"u8.ToArray());
                    throw new IOException("Simulated interrupted starter-media copy.");
                });

            await Assert.That(async () =>
                    await StarterMediaStager.StageAsync(environment, _ => failingStream))
                .Throws<IOException>();

            var finalPath = Path.Combine(hostWebRoot, "media", "data-center.png");
            await Assert.That(File.Exists(finalPath)).IsFalse();
            await Assert.That(Directory.EnumerateFiles(
                    Path.GetDirectoryName(finalPath)!,
                    "*.aerocms-staging-*.tmp"))
                .IsEmpty();

            var retried = await StarterMediaStager.StageAsync(environment);

            retried.ShouldBeTrue();
            (await File.ReadAllTextAsync(finalPath)).ShouldBe("complete-rcl-data");
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task CompleteAsync_RequiredHomepageFailure_DoesNotMarkSetupComplete()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>()
            .WithSchema<SiteHost>()
            .WithSchema<SitesModel>()
            .WithSchema<TenantModel>()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var tenant = new TenantModel { Id = 10_001, Name = "Starter Site" };
        var site = new SitesModel
        {
            Id = 20_001,
            TenantId = tenant.Id,
            Name = "Starter Site",
            IsEnabled = true,
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US"]
        };
        var identityBootstrapper = Substitute.For<ISetupIdentityBootstrapper>();
        identityBootstrapper
            .BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult
            {
                AdminUser = new AeroUser { Id = 30_001, UserName = "admin", Email = "admin@example.com" },
                CreatedAdmin = true,
                CreatedRoles = true
            });
        var pageContentService = Substitute.For<IPageContentService>();
        pageContentService
            .SaveAsync(Arg.Any<PageDocument>(), site.Id, Arg.Any<CancellationToken>())
            .Returns(Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError("page write failed")));
        var publishingWorkflow = Substitute.For<IPagePublishingWorkflowService>();
        var postContentService = Substitute.For<IPostContentService>();
        var bootstrapCompletionWriter = Substitute.For<IBootstrapCompletionWriter>();
        var tenantService = Substitute.For<ITenantService>();
        tenantService.CreateTenantAsync(Arg.Any<TenantModel>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<TenantModel, AeroError>(tenant));
        var siteService = Substitute.For<ISiteService>();
        siteService.CreateSiteAsync(Arg.Any<SitesModel>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<SitesModel, AeroError>(site));
        siteService.AddHostAsync(site.Id, Arg.Any<string>(), true, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<SiteHost, AeroError>(new SiteHost
            {
                Id = 40_001,
                SiteId = site.Id,
                Host = "localhost",
                IsPrimary = true
            }));
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.WebRootPath.Returns(Path.GetTempPath());

        var service = new SeedDatabaseService(
            harness.Session,
            environment,
            identityBootstrapper,
            pageContentService,
            publishingWorkflow,
            postContentService,
            Substitute.For<IMediaService>(),
            Substitute.For<ICommerceSeedService>(),
            Substitute.For<IModuleInitializationService>(),
            bootstrapCompletionWriter,
            tenantService,
            siteService,
            []);

        var result = await service.CompleteAsync(CreateRequest());

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Contains("homepage", StringComparison.OrdinalIgnoreCase));
        await pageContentService.Received(1).SaveAsync(
            Arg.Is<PageDocument>(page => page.SiteId == site.Id),
            site.Id,
            Arg.Any<CancellationToken>());
        await publishingWorkflow.DidNotReceiveWithAnyArgs()
            .PublishNowAsync(default, default, default);
        await bootstrapCompletionWriter.DidNotReceiveWithAnyArgs().MarkCompleteAsync(default);
        (await harness.Session.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId)).ShouldBeNull();
    }

    [Test]
    public async Task CompleteAsync_RetryAfterSecondPageFailure_ReusesPersistedHomepageIdentity()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<SetupStateDocument>()
            .WithSchema<SiteHost>()
            .WithSchema<SitesModel>()
            .WithSchema<TenantModel>()
            .WithSchema<PageDocument>();
        await harness.InitializeAsync();

        var tenant = new TenantModel { Id = 10_002, Name = "Starter Site" };
        var site = new SitesModel
        {
            Id = 20_002,
            TenantId = tenant.Id,
            Name = "Starter Site",
            IsEnabled = true,
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US"]
        };
        var identityBootstrapper = Substitute.For<ISetupIdentityBootstrapper>();
        identityBootstrapper
            .BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult
            {
                AdminUser = new AeroUser { Id = 30_002, UserName = "admin", Email = "admin@example.com" },
                CreatedAdmin = true,
                CreatedRoles = true
            });
        var homepageIds = new List<long>();
        var pageContentService = Substitute.For<IPageContentService>();
        pageContentService
            .SaveAsync(Arg.Any<PageDocument>(), site.Id, Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var page = callInfo.ArgAt<PageDocument>(0);
                if (page.Path == "/blog")
                {
                    return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError("blog write failed"));
                }

                homepageIds.Add(page.Id);
                harness.Session.Store(page);
                await harness.Session.SaveChangesAsync();
                return Prelude.Ok<PageDocument, AeroError>(page);
            });
        var publishingWorkflow = Substitute.For<IPagePublishingWorkflowService>();
        publishingWorkflow
            .PublishNowAsync(Arg.Any<long>(), site.Id, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<bool, AeroError>(true));
        var bootstrapCompletionWriter = Substitute.For<IBootstrapCompletionWriter>();
        var tenantService = Substitute.For<ITenantService>();
        tenantService.CreateTenantAsync(Arg.Any<TenantModel>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<TenantModel, AeroError>(tenant));
        var siteService = Substitute.For<ISiteService>();
        siteService.CreateSiteAsync(Arg.Any<SitesModel>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<SitesModel, AeroError>(site));
        siteService.AddHostAsync(site.Id, Arg.Any<string>(), true, Arg.Any<CancellationToken>())
            .Returns(Prelude.Ok<SiteHost, AeroError>(new SiteHost
            {
                Id = 40_002,
                SiteId = site.Id,
                Host = "localhost",
                IsPrimary = true
            }));
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.WebRootPath.Returns(Path.GetTempPath());
        var service = new SeedDatabaseService(
            harness.Session,
            environment,
            identityBootstrapper,
            pageContentService,
            publishingWorkflow,
            Substitute.For<IPostContentService>(),
            Substitute.For<IMediaService>(),
            Substitute.For<ICommerceSeedService>(),
            Substitute.For<IModuleInitializationService>(),
            bootstrapCompletionWriter,
            tenantService,
            siteService,
            []);

        var firstResult = await service.CompleteAsync(CreateRequest());
        var retryResult = await service.CompleteAsync(CreateRequest());

        firstResult.Succeeded.ShouldBeFalse();
        retryResult.Succeeded.ShouldBeFalse();
        homepageIds.Count.ShouldBe(2);
        homepageIds[1].ShouldBe(homepageIds[0]);
        await pageContentService.Received(2).SaveAsync(
            Arg.Is<PageDocument>(page => page.Path == "/blog"),
            site.Id,
            Arg.Any<CancellationToken>());
        var persistedPages = await harness.Session.Query<PageDocument>().ToListAsync();
        persistedPages.Count(page => page.SiteId == site.Id && page.Path == "/").ShouldBe(1);
        await bootstrapCompletionWriter.DidNotReceiveWithAnyArgs().MarkCompleteAsync(default);
    }

    private static SeedDatabaseRequest CreateRequest()
        => new(
            DatabaseMode: "embedded",
            CacheMode: "memory",
            SecretProvider: "local",
            RequestedManagerAuthenticationProvider: AuthenticationProviderSelections.Manager.Local,
            RequestedMemberAuthenticationProvider: AuthenticationProviderSelections.Member.Disabled,
            ConnectionString: null,
            CacheConnectionString: null,
            InfisicalMachineId: null,
            InfisicalClientSecret: null,
            AdminUserName: "admin",
            AdminEmail: "admin@example.com",
            Password: "not-persisted",
            SiteName: "Starter Site",
            HomepageTitle: "Welcome",
            BlogName: "News",
            Hostname: "localhost",
            DefaultCulture: "en-US",
            SupportedCultures: ["en-US"]);
}
