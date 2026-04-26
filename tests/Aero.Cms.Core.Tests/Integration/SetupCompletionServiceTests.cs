using TUnit.Core;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core.Entities;
using Aero.Models.Entities;
using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Tenant;
using Aero.Cms.Web.Core.Modules;
using Aero.Services.Images;
using FluentAssertions;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Integration;

public class SetupCompletionServiceTests
{
    [Test]
    public async Task Complete_async_seeds_starter_content_and_marks_setup_complete()
    {
        var harness = new InMemoryCmsDocumentSessionHarness();
        var identityBootstrapper = Substitute.For<ISetupIdentityBootstrapper>();
        identityBootstrapper.BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult 
            { 
                CreatedAdmin = true, 
                CreatedRoles = true,
                AdminUser = new AeroUser { Id = 12345 } 
            });

        var service = CreateService(harness, identityBootstrapper);

        var result = await service.CompleteAsync(CreateRequest());

        result.Succeeded.Should().BeTrue();
        harness.SetupStates.Should().ContainKey(SetupStateDocument.FixedId);
        harness.SetupStates[SetupStateDocument.FixedId].IsComplete.Should().BeTrue();
        harness.SetupStates[SetupStateDocument.FixedId].CompletedAtUtc.Should().NotBeNull();

        harness.Pages.Should().HaveCount(2);
        var homepage = harness.Pages.Values.First(p => p.Slug == "/");
        homepage.Slug.Should().Be("/");
        homepage.Title.Should().Be("Welcome to Aero CMS");
        homepage.PublicationState.Should().Be(ContentPublicationState.Published);
        var blogListing = harness.Pages.Values.First(p => p.Slug == "blog");
        blogListing.Slug.Should().Be("blog");
        blogListing.Title.Should().Be("Field Notes");
        blogListing.PublicationState.Should().Be(ContentPublicationState.Published);

        harness.BlogPosts.Should().HaveCount(3);
        harness.BlogPosts.Keys.Should().BeEquivalentTo(
            "cms/posts/getting-started-with-aero-cms",
            "cms/posts/shaping-your-homepage-message",
            "cms/posts/publishing-your-first-update");
        harness.BlogPosts.Values.Should()
            .OnlyContain(post => post.PublicationState == ContentPublicationState.Published);
    }

    [Test]
    public async Task Complete_async_leaves_setup_incomplete_on_seed_failure_and_retry_is_idempotent()
    {
        var harness = new InMemoryCmsDocumentSessionHarness();
        var identityBootstrapper = Substitute.For<ISetupIdentityBootstrapper>();
        identityBootstrapper.BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SetupIdentityBootstrapResult 
            { 
                CreatedAdmin = true,
                AdminUser = new AeroUser { Id = 12345 } 
            });

        harness.OnStore = stored =>
        {
            if (stored is BlogPostDocument { Slug: "publishing-your-first-update" })
            {
                throw new InvalidOperationException("Simulated seed failure.");
            }
        };

        var service = CreateService(harness, identityBootstrapper);

        var failedResult = await service.CompleteAsync(CreateRequest());

        failedResult.Succeeded.Should().BeFalse();
        failedResult.Errors.Should().ContainSingle(error => error == "Simulated seed failure.");
        harness.SetupStates.Should().BeEmpty();
        harness.Pages.Should().HaveCount(2);
        harness.BlogPosts.Should().HaveCount(2);

        harness.OnStore = null;

        var retriedResult = await service.CompleteAsync(CreateRequest());

        retriedResult.Succeeded.Should().BeTrue();
        harness.SetupStates.Should().ContainKey(SetupStateDocument.FixedId);
        harness.SetupStates[SetupStateDocument.FixedId].IsComplete.Should().BeTrue();
        harness.Pages.Should().HaveCount(2);
        harness.BlogPosts.Should().HaveCount(3);

        await identityBootstrapper.Received(2)
            .BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Complete_async_short_circuits_when_setup_is_already_complete()
    {
        var harness = new InMemoryCmsDocumentSessionHarness();
        harness.Session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true,
            CompletedAtUtc = DateTimeOffset.UtcNow
        });

        var identityBootstrapper = Substitute.For<ISetupIdentityBootstrapper>();
        var service = CreateService(harness, identityBootstrapper);

        var result = await service.CompleteAsync(CreateRequest());

        result.Succeeded.Should().BeTrue();
        result.AlreadyComplete.Should().BeTrue();
        await identityBootstrapper.DidNotReceive()
            .BootstrapAsync(Arg.Any<SetupIdentityBootstrapRequest>(), Arg.Any<CancellationToken>());
        harness.Pages.Should().BeEmpty();
        harness.BlogPosts.Should().BeEmpty();
    }

    private static SeedDatabaseService CreateService(InMemoryCmsDocumentSessionHarness harness,
        ISetupIdentityBootstrapper identityBootstrapper)
    {
        var tenantService = Substitute.For<ITenantService>();
        tenantService.CreateTenantAsync(Arg.Any<TenantModel>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<TenantModel>());

        var siteService = Substitute.For<ISiteService>();
        siteService.CreateSiteAsync(Arg.Any<SitesModel>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<SitesModel>());

        var apiKeyService = Substitute.For<IApiKeyService>();
        apiKeyService.CreateKeyAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("mock-key"));

        return new SeedDatabaseService(
            harness.Session,
            identityBootstrapper,
            new MartenPageContentService(harness.Session, Substitute.For<IBlockService>(),
                Substitute.For<IMessageBus>()),
            new MartenBlogPostContentService(harness.Session),
            Substitute.For<IStaticPhotosClient>(),
            Substitute.For<IModuleDiscoveryService>(),
            Substitute.For<IModuleStateStore>(),
            Substitute.For<IBootstrapCompletionWriter>(),
            tenantService,
            siteService,
            apiKeyService);
    }

    private static SeedDatabaseRequest CreateRequest()
        => new(
            "embedded",
            "memory",
            "environment",
            "Local",
            null,
            null,
            null,
            null,
            "admin.user",
            "admin@example.com",
            "CorrectHorseBattery1!",
            "Aero CMS",
            "Welcome to Aero CMS",
            "Field Notes",
            "localhost",
            "en-US");
}
