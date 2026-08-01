using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Html;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;
using Wolverine;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class SiteStyleProfileServiceTests
{
    [Test]
    public async Task Update_normalized_no_op_keeps_revision_and_does_not_publish()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = new SiteStyleProfileService(harness.Store, bus);

        var result = await service.UpdateAsync(91, new UpdateSiteStyleProfileRequest(
            2,
            48,
            [
                new SiteStyleColorTokenViewModel
                {
                    Name = "Brand Primary",
                    HexValue = "#abc"
                }
            ]));

        var ok = result as Result<SiteStyleProfileViewModel, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.Revision).IsEqualTo(2);
        await bus.DidNotReceive().PublishAsync(Arg.Any<SiteStyleProfileChangedEvent>());

        await using var verification = await harness.OpenSessionAsync();
        var saved = (await verification.Query<SitesModel>().ToListAsync())
            .SingleOrDefault(site => site.Id == 91);
        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.StyleProfile.Revision).IsEqualTo(2);
        await Assert.That(saved.ModifiedOn).IsNull();
    }

    [Test]
    public async Task Update_semantic_change_increments_once_persists_canonical_values_and_publishes()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = new SiteStyleProfileService(harness.Store, bus);

        var result = await service.UpdateAsync(91, new UpdateSiteStyleProfileRequest(
            2,
            54,
            [
                new SiteStyleColorTokenViewModel
                {
                    Name = "Accent Color",
                    HexValue = "#1234"
                }
            ]));

        var ok = result as Result<SiteStyleProfileViewModel, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.Revision).IsEqualTo(3);
        await Assert.That(ok.Value.ColorTokens[0].Name).IsEqualTo("accent-color");
        await Assert.That(ok.Value.ColorTokens[0].HexValue).IsEqualTo("#11223344");
        await bus.Received(1).PublishAsync(Arg.Is<SiteStyleProfileChangedEvent>(
            message => message.SiteId == 91 && message.Revision == 3));

        await using var verification = await harness.OpenSessionAsync();
        var saved = (await verification.Query<SitesModel>().ToListAsync())
            .SingleOrDefault(site => site.Id == 91);
        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.StyleProfile.Revision).IsEqualTo(3);
        await Assert.That(saved.StyleProfile.SmallScreenBreakpointRem).IsEqualTo(54);
        await Assert.That(saved.StyleProfile.ColorTokens[0].Name).IsEqualTo("accent-color");
    }

    [Test]
    public async Task Update_returns_conflict_for_stale_revision_and_not_found_for_missing_site()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = new SiteStyleProfileService(harness.Store, bus);
        var request = new UpdateSiteStyleProfileRequest(1, 50, []);

        var stale = await service.UpdateAsync(91, request);
        var missing = await service.UpdateAsync(404, request);

        await Assert.That(stale).IsTypeOf<Result<SiteStyleProfileViewModel, AeroError>.Failure>();
        await Assert.That(((Result<SiteStyleProfileViewModel, AeroError>.Failure)stale).Error)
            .IsTypeOf<AeroError.Conflict>();
        await Assert.That(missing).IsTypeOf<Result<SiteStyleProfileViewModel, AeroError>.Failure>();
        await Assert.That(((Result<SiteStyleProfileViewModel, AeroError>.Failure)missing).Error)
            .IsTypeOf<AeroError.NotFound>();
        await bus.DidNotReceive().PublishAsync(Arg.Any<SiteStyleProfileChangedEvent>());
    }

    [Test]
    public async Task Concurrent_updates_from_same_revision_allow_exactly_one_writer()
    {
        await using var harness = await CreateHarnessAsync();
        var bus = Substitute.For<IMessageBus>();
        var service = new SiteStyleProfileService(harness.Store, bus);

        var first = service.UpdateAsync(91, new UpdateSiteStyleProfileRequest(
            2,
            52,
            [new SiteStyleColorTokenViewModel { Name = "brand-primary", HexValue = "#112233" }]));
        var second = service.UpdateAsync(91, new UpdateSiteStyleProfileRequest(
            2,
            56,
            [new SiteStyleColorTokenViewModel { Name = "brand-primary", HexValue = "#445566" }]));

        var results = await Task.WhenAll(first, second);
        var successes = results.Count(static result =>
            result is Result<SiteStyleProfileViewModel, AeroError>.Ok);
        var failure = results.OfType<Result<SiteStyleProfileViewModel, AeroError>.Failure>().Single();

        await Assert.That(successes).IsEqualTo(1);
        await Assert.That(failure.Error).IsTypeOf<AeroError.Conflict>();
        await bus.Received(1).PublishAsync(Arg.Any<SiteStyleProfileChangedEvent>());

        await using var verification = await harness.OpenSessionAsync();
        var saved = (await verification.Query<SitesModel>().ToListAsync())
            .Single(site => site.Id == 91);
        await Assert.That(saved.StyleProfile.Revision).IsEqualTo(3);
        await Assert.That(saved.StyleProfile.SmallScreenBreakpointRem is 52 or 56).IsTrue();
    }

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Strict)
            .WithConfiguration(options =>
                options.Schema.For<SitesModel>().UseOptimisticConcurrency = true);
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel
        {
            Id = 91,
            TenantId = 7,
            Name = "Style Test",
            IsEnabled = true,
            StyleProfile = new StyleProfileSettings
            {
                Revision = 2,
                SmallScreenBreakpointRem = 48,
                ColorTokens =
                [
                    new StyleColorToken
                    {
                        Name = "brand-primary",
                        HexValue = "#aabbcc"
                    }
                ]
            }
        });
        await harness.Session.SaveChangesAsync();
        return harness;
    }
}
