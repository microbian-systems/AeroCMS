using Aero.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Modules.Pages.Grains;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AeroPageGrainScopeTests
{
    [Test]
    public async Task InheritedIdentifierOnlyCrudMethods_FailClosedWithoutOpeningSession()
    {
        var store = Substitute.For<IDocumentStore>();
        var grain = new AeroPageGrain(
            Substitute.For<ILogger<AeroActor>>(),
            store,
            Substitute.For<IServiceProvider>());
        var update = new UpdatePageRequest(
            901,
            "Title",
            "title",
            null,
            null,
            null,
            ContentPublicationState.Draft);

        var get = await grain.GetByIdAsync(901, CancellationToken.None);
        var getMany = await grain.GetByIdsAsync([901], CancellationToken.None);
        var updateResult = await grain.UpdateAsync(update, CancellationToken.None);
        var delete = await grain.DeleteAsync(new DeletePageRequest(901), CancellationToken.None);

        await Assert.That(get.error.Message).IsNotEmpty();
        await Assert.That(getMany.error.Message).IsNotEmpty();
        await Assert.That(updateResult.error.Message).IsNotEmpty();
        await Assert.That(delete.error.Message).IsNotEmpty();
        await store.DidNotReceive().QuerySessionAsync();
        await store.DidNotReceive().LightweightSessionAsync();
    }
}
