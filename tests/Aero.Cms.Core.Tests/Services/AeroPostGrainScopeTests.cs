using Aero.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Modules.Posts.Grains;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AeroPostGrainScopeTests
{
    [Test]
    public async Task InheritedIdentifierOnlyCrudMethods_FailClosedWithoutOpeningSession()
    {
        var store = Substitute.For<IDocumentStore>();
        var grain = new AeroPostGrain(
            Substitute.For<ILogger<AeroActor>>(),
            store,
            Substitute.For<IServiceProvider>());
        var update = new UpdatePostRequest(
            901,
            "Title",
            "title",
            null,
            null,
            null,
            null,
            null,
            ContentPublicationState.Draft);

        var get = await grain.GetByIdAsync(901, CancellationToken.None);
        var getMany = await grain.GetByIdsAsync([901], CancellationToken.None);
        var create = await grain.CreateAsync(
            new CreatePostRequest(
                "Title",
                "title",
                null,
                null,
                null,
                null,
                null,
                ContentPublicationState.Draft,
                42),
            CancellationToken.None);
        var updateResult = await grain.UpdateAsync(update, CancellationToken.None);
        var delete = await grain.DeleteAsync(new DeletePostRequest(901), CancellationToken.None);

        await Assert.That(get.error.Message).IsNotEmpty();
        await Assert.That(getMany.error.Message).IsNotEmpty();
        await Assert.That(create.error.Message).IsNotEmpty();
        await Assert.That(updateResult.error.Message).IsNotEmpty();
        await Assert.That(delete.error.Message).IsNotEmpty();
        await store.DidNotReceive().QuerySessionAsync();
        await store.DidNotReceive().LightweightSessionAsync();
    }
}
