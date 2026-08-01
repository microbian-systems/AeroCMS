using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Services;

public sealed class DocsTreeServiceTests
{
    private const long SiteId = 701;
    private const long ForeignSiteId = 702;
    private const long SpaceId = 711;
    private const long ParentId = 712;
    private const long FirstId = 713;
    private const long SecondId = 714;

    [Test]
    public async Task Reorder_EmptyRequestStillRejectsMissingOrForeignParent()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateService(harness.Session);

        var missing = await service.ReorderSiblingsAsync(SiteId, SpaceId, 999, [], CancellationToken.None);
        var foreign = await service.ReorderSiblingsAsync(SiteId, SpaceId, 799, [], CancellationToken.None);

        await Assert.That(missing.IsFailure).IsTrue();
        await Assert.That(foreign.IsFailure).IsTrue();
    }

    [Test]
    public async Task Reorder_DuplicateOrWrongParent_IsRejectedWithoutChangingOrder()
    {
        await using var harness = await CreateHarnessAsync();
        var service = CreateService(harness.Session);

        var duplicate = await service.ReorderSiblingsAsync(SiteId, SpaceId, ParentId, [FirstId, FirstId], CancellationToken.None);
        var wrongParent = await service.ReorderSiblingsAsync(SiteId, SpaceId, ParentId, [FirstId, 715], CancellationToken.None);

        await Assert.That(duplicate.IsFailure).IsTrue();
        await Assert.That(wrongParent.IsFailure).IsTrue();
        await Assert.That((await harness.Session.LoadAsync<DocsPage>(FirstId))!.Order).IsEqualTo(0);
        await Assert.That((await harness.Session.LoadAsync<DocsPage>(SecondId))!.Order).IsEqualTo(1);
    }

    [Test]
    public async Task Reorder_ValidSiblingOrder_Persists()
    {
        await using var harness = await CreateHarnessAsync();
        var result = await CreateService(harness.Session).ReorderSiblingsAsync(SiteId, SpaceId, ParentId, [SecondId, FirstId], CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That((await harness.Session.LoadAsync<DocsPage>(SecondId))!.Order).IsEqualTo(0);
        await Assert.That((await harness.Session.LoadAsync<DocsPage>(FirstId))!.Order).IsEqualTo(1);
    }

    private static DocsTreeService CreateService(IDocumentSession session)
        => new(session, Substitute.For<IMessageBus>(), Substitute.For<ILogger<DocsTreeService>>());

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithSchema<DocsPage>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            new DocsPage { Id = SpaceId, SiteId = SiteId, Title = "Docs", Slug = "docs" },
            new DocsPage { Id = ParentId, SiteId = SiteId, ParentId = SpaceId, Title = "Parent", Slug = "docs/parent" },
            new DocsPage { Id = FirstId, SiteId = SiteId, ParentId = ParentId, Title = "First", Slug = "docs/parent/first", Order = 0 },
            new DocsPage { Id = SecondId, SiteId = SiteId, ParentId = ParentId, Title = "Second", Slug = "docs/parent/second", Order = 1 },
            new DocsPage { Id = 715, SiteId = SiteId, ParentId = SpaceId, Title = "Elsewhere", Slug = "docs/elsewhere" },
            new DocsPage { Id = 799, SiteId = ForeignSiteId, Title = "Foreign", Slug = "foreign" });
        await harness.Session.SaveChangesAsync();
        return harness;
    }
}
