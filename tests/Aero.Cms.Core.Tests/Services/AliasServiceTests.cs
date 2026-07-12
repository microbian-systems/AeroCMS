using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases;
using Aero.Cms.Modules.Aliases.Events;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AliasServiceTests
{
    private SableTestHarness _harness = null!;
    private IAliasRepository _repository = null!;
    private IMessageBus _bus = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _harness = new SableTestHarness()
            .WithSchema<AliasDocument>();
        await _harness.InitializeAsync();
        _repository = Substitute.For<IAliasRepository>();
        _bus = Substitute.For<IMessageBus>();
    }

    [After(Test)]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_CommitsAliasBeforePublishingInvalidation()
    {
        var session = _harness.Session;
        var service = new AliasService(_repository, session, _bus);
        var alias = new AliasDocument
        {
            Id = 1501688860171780096,
            SiteId = 42,
            OldPath = "/old",
            NewPath = "/new"
        };

        // Make the mock repository store into the real session
        _repository.AddAsync(Arg.Any<AliasDocument>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var doc = callInfo.Arg<AliasDocument>();
                session.Store(doc);
                return Task.CompletedTask;
            });

        await service.CreateAsync(alias, CancellationToken.None);

        // Verify alias was stored in the real DB (proves SaveChangesAsync was called after AddAsync)
        var stored = await session.Query<AliasDocument>()
            .FirstOrDefaultAsync(x => x.Id == 1501688860171780096);
        stored.ShouldNotBeNull();
        stored!.OldPath.ShouldBe("/old");
        stored.NewPath.ShouldBe("/new");

        // Verify bus published the event (proves PublishAsync was called after SaveChangesAsync)
        await _bus.Received(1).PublishAsync(
            Arg.Is<AliasCreated>(e => e.Document == alias));
    }
}
