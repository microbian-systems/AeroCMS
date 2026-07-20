using Aero.Actors;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Aliases.Grains;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AeroAliasGrainScopeTests
{
    [Test]
    public async Task InheritedCrud_FailsClosedWithoutOpeningSession()
    {
        var store = Substitute.For<IDocumentStore>();
        var grain = new AeroAliasGrain(Substitute.For<ILogger<AeroActor>>(), store, Substitute.For<IMessageBus>());
        var createRequest = new CreateAliasRequest(99, "/old", "/new");
        var get = await grain.GetByIdAsync(1, default);
        var many = await grain.GetByIdsAsync([1], default);
        var create = await grain.CreateAsync(createRequest, default);
        var update = await grain.UpdateAsync(new UpdateAliasRequest(1, "/old", "/new"), default);
        var delete = await grain.DeleteAsync(new DeleteAliasRequest(1), default);
        await Assert.That(new[] { get, many, create, update, delete }.All(x => !string.IsNullOrWhiteSpace(x.error.Message))).IsTrue();
        await store.DidNotReceive().LightweightSessionAsync();
        await store.DidNotReceive().QuerySessionAsync();
    }

    [Test]
    public async Task ScopedOperations_UseSuppliedSiteAndPreserveForeignAlias()
    {
        await using var harness = new SableTestHarness().WithSchema<AliasDocument>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new AliasDocument { Id = 1, SiteId = 10, OldPath = "/local", NormalizedOldPath = "/local", NewPath = "/new" },
            new AliasDocument { Id = 2, SiteId = 20, OldPath = "/foreign", NormalizedOldPath = "/foreign", NewPath = "/other" });
        await harness.Session.SaveChangesAsync();
        var grain = new AeroAliasGrain(Substitute.For<ILogger<AeroActor>>(), harness.Store, Substitute.For<IMessageBus>());

        var local = await grain.GetByIdAsync(1, 10, default);
        var foreign = await grain.GetByIdAsync(2, 10, default);
        var siteAliases = await grain.GetAllAliasesAsync(10, default);
        var created = await grain.CreateAliasAsync(new CreateAliasRequest(999, "/created", "/target"), 10, default);
        var deleteForeign = await grain.DeleteAliasAsync(2, 10, default);
        var deleteLocal = await grain.DeleteAliasAsync(1, 10, default);

        await Assert.That(local.error.Message).IsNull();
        await Assert.That(foreign.error.Message).IsNotEmpty();
        await Assert.That(siteAliases.Select(x => x.Id)).IsEquivalentTo([1L]);
        await Assert.That(created.data.SiteId).IsEqualTo(10);
        await Assert.That(deleteForeign.error.Message).IsNotEmpty();
        await Assert.That(deleteLocal.error.Message).IsNull();
        await using var verify = await harness.Store.QuerySessionAsync();
        await Assert.That(await verify.LoadAsync<AliasDocument>(1)).IsNull();
        await Assert.That(await verify.LoadAsync<AliasDocument>(2)).IsNotNull();
    }
}
