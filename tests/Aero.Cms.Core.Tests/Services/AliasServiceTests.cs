using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases;
using Aero.Cms.Modules.Aliases.Events;
using AeroDB.Sable;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AliasServiceTests
{
    [Test]
    public async Task CreateAsync_CommitsAliasBeforePublishingInvalidation()
    {
        var repository = Substitute.For<IAliasRepository>();
        var session = Substitute.For<IDocumentSession>();
        var bus = Substitute.For<IMessageBus>();
        var service = new AliasService(repository, session, bus);
        var alias = new AliasDocument
        {
            Id = 1501688860171780096,
            SiteId = 42,
            OldPath = "/old",
            NewPath = "/new"
        };

        session.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var calls = new List<string>();
        repository.AddAsync(alias, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("add");
                return Task.CompletedTask;
            });
        session.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("save");
                return Task.CompletedTask;
            });
        bus.PublishAsync(Arg.Is<AliasCreated>(e => e.Document == alias))
            .Returns(_ =>
            {
                calls.Add("publish");
                return ValueTask.CompletedTask;
            });

        await service.CreateAsync(alias, CancellationToken.None);

        await Assert.That(calls).IsEqualTo(["add", "save", "publish"]);
    }
}
