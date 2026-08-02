using System.Security.Claims;
using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Theming;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Theming;

public sealed class ThemeApplicationServicePersistenceTests
{
    [Test]
    public async Task Publish_is_idempotent_and_public_identity_survives_slug_changes()
    {
        await using var fixture = await ThemeFixture.CreateAsync();
        var service = await fixture.CreateServiceAsync();
        var first = await service.CreateAsync(new CreateThemeCommand("First", "first", null));

        var published = await service.PublishAsync(first.Id);
        var retry = await service.PublishAsync(first.Id);

        retry.ThemeId.ShouldBe(published.ThemeId);
        retry.Version.ShouldBe(published.Version);
        retry.Sha256.ShouldBe(published.Sha256);

        var renamed = await service.SaveDraftAsync(
            first.Id,
            new SaveThemeDraftCommand(first.Revision, "First renamed", "renamed", null, first.Tokens));
        var replacement = await service.CreateAsync(new CreateThemeCommand("Replacement", "first", null));
        var replacementVersion = await service.PublishAsync(replacement.Id);

        renamed.Slug.ShouldBe("renamed");
        replacementVersion.ThemeId.ShouldNotBe(published.ThemeId);

        await using var read = await fixture.Harness.OpenSessionAsync();
        var versions = await read.Query<ThemeVersionDocument>().ToListAsync();
        versions.Count.ShouldBe(2);
        versions.Select(x => x.ThemeDefinitionId).Distinct().Count().ShouldBe(2);
    }

    [Test]
    public async Task Concurrent_publish_returns_one_matching_immutable_artifact()
    {
        await using var fixture = await ThemeFixture.CreateAsync();
        var creator = await fixture.CreateServiceAsync();
        var theme = await creator.CreateAsync(new CreateThemeCommand("Concurrent", "concurrent", null));
        var firstService = await fixture.CreateServiceAsync();
        var secondService = await fixture.CreateServiceAsync();

        var results = await Task.WhenAll(
            firstService.PublishAsync(theme.Id),
            secondService.PublishAsync(theme.Id));

        results[0].ThemeId.ShouldBe(results[1].ThemeId);
        results[0].Version.ShouldBe(results[1].Version);
        results[0].Sha256.ShouldBe(results[1].Sha256);
        await using var read = await fixture.Harness.OpenSessionAsync();
        (await read.Query<ThemeVersionDocument>().ToListAsync()).Count.ShouldBe(1);
    }

    [Test]
    public async Task Assignment_retry_with_original_revision_returns_the_original_publication()
    {
        await using var fixture = await ThemeFixture.CreateAsync();
        var service = await fixture.CreateServiceAsync();
        var theme = await service.CreateAsync(new CreateThemeCommand("Assigned", "assigned", null));
        var version = await service.PublishAsync(theme.Id);
        fixture.Resolve(version);

        var assigned = await service.AssignAsync(new AssignThemeCommand(version.ThemeId, version.Version, 1));
        var retry = await service.AssignAsync(new AssignThemeCommand(version.ThemeId, version.Version, 1));

        AssertSamePublication(retry, assigned);
        await using var read = await fixture.Harness.OpenSessionAsync();
        (await read.Query<SiteThemePublicationDocument>().ToListAsync()).Count.ShouldBe(1);
        (await read.LoadAsync<SitesModel>(fixture.SiteId))!.ThemeRevision.ShouldBe(2);
    }

    [Test]
    public async Task Concurrent_assignment_losers_observe_the_successful_exact_target()
    {
        await using var fixture = await ThemeFixture.CreateAsync();
        var creator = await fixture.CreateServiceAsync();
        var theme = await creator.CreateAsync(new CreateThemeCommand("Concurrent assignment", "concurrent-assignment", null));
        var version = await creator.PublishAsync(theme.Id);
        fixture.Resolve(version);
        var firstService = await fixture.CreateServiceAsync();
        var secondService = await fixture.CreateServiceAsync();

        var results = await Task.WhenAll(
            firstService.AssignAsync(new AssignThemeCommand(version.ThemeId, version.Version, 1)),
            secondService.AssignAsync(new AssignThemeCommand(version.ThemeId, version.Version, 1)));

        AssertSamePublication(results[0], results[1]);
        await using var read = await fixture.Harness.OpenSessionAsync();
        (await read.Query<SiteThemePublicationDocument>().ToListAsync()).Count.ShouldBe(1);
        (await read.LoadAsync<SitesModel>(fixture.SiteId))!.ThemeRevision.ShouldBe(2);
    }

    [Test]
    public async Task Concurrent_slug_creation_has_one_winner_and_one_domain_conflict()
    {
        await using var fixture = await ThemeFixture.CreateAsync();
        var firstService = await fixture.CreateServiceAsync();
        var secondService = await fixture.CreateServiceAsync();

        var attempts = await Task.WhenAll(
            CaptureAsync(() => firstService.CreateAsync(new CreateThemeCommand("One", "shared", null))),
            CaptureAsync(() => secondService.CreateAsync(new CreateThemeCommand("Two", "shared", null))));

        attempts.Count(x => x is ThemeDefinitionView).ShouldBe(1);
        attempts.Count(x => x is ThemeConflictException).ShouldBe(1);
        await using var read = await fixture.Harness.OpenSessionAsync();
        (await read.Query<ThemeDefinitionDocument>().ToListAsync()).Count.ShouldBe(1);
    }

    private static async Task<object> CaptureAsync(Func<Task<ThemeDefinitionView>> action)
    {
        try
        {
            return await action();
        }
        catch (ThemeConflictException exception)
        {
            return exception;
        }
    }

    private static void AssertSamePublication(
        SiteThemePublicationView actual,
        SiteThemePublicationView expected)
    {
        actual.ThemeId.ShouldBe(expected.ThemeId);
        actual.Version.ShouldBe(expected.Version);
        actual.Revision.ShouldBe(expected.Revision);
        actual.PreviousThemeId.ShouldBe(expected.PreviousThemeId);
        actual.PreviousVersion.ShouldBe(expected.PreviousVersion);
        actual.PublishedOn.ToUnixTimeSeconds().ShouldBe(expected.PublishedOn.ToUnixTimeSeconds());
    }

    private sealed class ThemeFixture : IAsyncDisposable
    {
        private readonly List<IQuerySession> _querySessions = [];
        private readonly IThemeLibrary _library = Substitute.For<IThemeLibrary>();
        private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        private ThemeFixture(SableTestHarness harness, long siteId)
        {
            Harness = harness;
            SiteId = siteId;
        }

        public SableTestHarness Harness { get; }
        public long SiteId { get; }

        public static async Task<ThemeFixture> CreateAsync()
        {
            var module = new AeroThemeModule();
            var harness = new SableTestHarness()
                .WithConfiguration(options =>
                {
                    module.Configure(options);
                    var sites = options.Schema.For<SitesModel>()
                        .TableName(Schemas.Tables.Sites);
                    sites.UseOptimisticConcurrency = true;
                });
            await harness.InitializeAsync();
            const long siteId = 91_001;
            harness.Session.Store(new SitesModel
            {
                Id = siteId,
                TenantId = 71_001,
                Name = "Theme test site",
                IsEnabled = true
            });
            await harness.Session.SaveChangesAsync();
            return new ThemeFixture(harness, siteId);
        }

        public async Task<IThemeApplicationService> CreateServiceAsync()
        {
            var querySession = await Harness.OpenSessionAsync();
            _querySessions.Add(querySession);
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "81"), new Claim(ClaimTypes.Role, "Admin")],
                    "theme-test"))
            };
            httpContext.Request.Headers.Cookie = $"AeroCms.SiteId={SiteId}";
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(httpContext);
            return new ThemeApplicationService(
                Harness.Store,
                querySession,
                _library,
                new ThemeCssCompiler(),
                new ThemeDesignContextAccessor(accessor, querySession),
                _cache,
                _messageBus,
                NullLogger<ThemeApplicationService>.Instance);
        }

        public void Resolve(ThemeVersionView version)
        {
            _library.ResolveAsync(
                    71_001,
                    version.ThemeId,
                    version.Version,
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult<ResolvedThemeManifest?>(new ResolvedThemeManifest(
                    version.ThemeId,
                    version.Version,
                    version.DataThemeName,
                    ThemeSource.Generated,
                    [])));
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var session in _querySessions)
                await session.DisposeAsync();
            _cache.Dispose();
            await Harness.DisposeAsync();
        }
    }
}
