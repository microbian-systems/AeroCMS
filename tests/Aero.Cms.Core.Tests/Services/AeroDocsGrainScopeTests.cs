using Aero.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs.Grains;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AeroDocsGrainScopeTests
{
    [Test]
    public async Task InheritedIdentifierOnlyCrudMethods_FailClosedWithoutOpeningSession()
    {
        var store = Substitute.For<IDocumentStore>();
        var grain = new AeroDocsGrain(Substitute.For<ILogger<AeroActor>>(), store, Substitute.For<IServiceProvider>());
        var update = new UpdatePageRequest(901, "Title", "title", null, null, null, ContentPublicationState.Draft);

        var get = await grain.GetByIdAsync(901, CancellationToken.None);
        var getMany = await grain.GetByIdsAsync([901], CancellationToken.None);
        var create = await grain.CreateAsync(update, CancellationToken.None);
        var changed = await grain.UpdateAsync(update, CancellationToken.None);
        var deleted = await grain.DeleteAsync(new DeletePageRequest(901), CancellationToken.None);

        await Assert.That(get.error.Message).IsNotEmpty();
        await Assert.That(getMany.error.Message).IsNotEmpty();
        await Assert.That(create.error.Message).IsNotEmpty();
        await Assert.That(changed.error.Message).IsNotEmpty();
        await Assert.That(deleted.error.Message).IsNotEmpty();
        await store.DidNotReceive().QuerySessionAsync();
        await store.DidNotReceive().LightweightSessionAsync();
    }

    [Test]
    public async Task ScopedGetAndDelete_ConcealForeignDocAndPreserveIt()
    {
        await using var harness = new SableTestHarness().WithSchema<DocsPage>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new DocsPage { Id = 1001, SiteId = 10, Title = "Local", Slug = "local" }, new DocsPage { Id = 1002, SiteId = 20, Title = "Foreign", Slug = "foreign" });
        await harness.Session.SaveChangesAsync();
        var grain = new AeroDocsGrain(Substitute.For<ILogger<AeroActor>>(), harness.Store, CreateServices());

        var local = await grain.GetByIdAsync(1001, 10, CancellationToken.None);
        var foreign = await grain.GetByIdAsync(1002, 10, CancellationToken.None);
        var deleteForeign = await grain.DeleteDocAsync(1002, 10, CancellationToken.None);

        await Assert.That(local.error.Message).IsNull();
        await Assert.That(foreign.error.Message).IsNotEmpty();
        await Assert.That(deleteForeign.error.Message).IsNotEmpty();
    }

    private static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IMessageBus>());
        services.AddSingleton(Substitute.For<ILogger<Aero.Cms.Modules.Docs.DocsContentService>>());
        services.AddSingleton(Substitute.For<ILogger<Aero.Cms.Modules.Docs.DocsTreeService>>());
        return services.BuildServiceProvider();
    }
}
