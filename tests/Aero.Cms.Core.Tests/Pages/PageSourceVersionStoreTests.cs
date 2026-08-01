using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Pages;

public sealed class PageSourceVersionStoreTests
{
    [Test]
    public async Task Stage_preserves_exact_source_and_leaves_commit_to_the_owning_unit_of_work()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var source = "\r\n  <h1>@Model.Title</h1>\n";
        var createdOn = new DateTimeOffset(2026, 7, 24, 14, 30, 0, TimeSpan.Zero);
        var store = new PageSourceVersionStore(harness.Session);

        var result = store.Stage(new PageSourceVersionWriteRequest(
            11,
            21,
            "AERO.SCRIBAN",
            source,
            createdOn,
            "editor@example.test"));

        var staged = result as Result<PageSourceVersionSnapshot>.Ok;
        await Assert.That(staged).IsNotNull();
        await Assert.That(staged!.Value.Id).IsGreaterThan(0);
        await Assert.That(staged.Value.Source).IsEqualTo(source);
        await Assert.That(staged.Value.RendererId).IsEqualTo(PageRendererIds.Scriban);
        await Assert.That(staged.Value.SourceHash)
            .IsEqualTo("978bbd9b8f121af3e6275a0e1edfe23ec302b68872cabb76e7d5196fd374a25b");

        await using (var beforeCommit = await harness.OpenSessionAsync())
        {
            var notCommitted = await beforeCommit.LoadAsync<PageSourceVersion>(staged.Value.Id);
            await Assert.That(notCommitted).IsNull();
        }

        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var verificationStore = new PageSourceVersionStore(verificationSession);
        var loadedResult = await verificationStore.LoadAsync(
            staged.Value.Id,
            11,
            21,
            PageRendererIds.Scriban);
        var loaded = loadedResult as Result<PageSourceVersionSnapshot?>.Ok;

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Value).IsNotNull();
        await Assert.That(loaded.Value!.Source).IsEqualTo(source);
        await Assert.That(loaded.Value.SourceHash).IsEqualTo(staged.Value.SourceHash);
        await Assert.That(loaded.Value.CreatedOn).IsEqualTo(createdOn);
        await Assert.That(loaded.Value.CreatedBy).IsEqualTo("editor@example.test");
    }

    [Test]
    public async Task Load_fails_closed_when_source_version_ownership_does_not_match()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var writer = new PageSourceVersionStore(harness.Session);
        var staged = (Result<PageSourceVersionSnapshot>.Ok)writer.Stage(
            new PageSourceVersionWriteRequest(
                11,
                21,
                PageRendererIds.Scriban,
                "{{ page.title }}",
                DateTimeOffset.UtcNow));
        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var reader = new PageSourceVersionStore(verificationSession);
        var wrongSite = await reader.LoadAsync(staged.Value.Id, 12, 21, PageRendererIds.Scriban);
        var wrongPage = await reader.LoadAsync(staged.Value.Id, 11, 22, PageRendererIds.Scriban);
        var wrongRenderer = await reader.LoadAsync(staged.Value.Id, 11, 21, PageRendererIds.SharpTs);

        await AssertNotFound(wrongSite);
        await AssertNotFound(wrongPage);
        await AssertNotFound(wrongRenderer);
    }

    [Test]
    public async Task Load_returns_successful_absence_for_an_empty_source_pointer()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<PageSourceVersion>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var store = new PageSourceVersionStore(harness.Session);

        var result = await store.LoadAsync(null, 0, 0, string.Empty);
        var success = result as Result<PageSourceVersionSnapshot?>.Ok;

        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value).IsNull();
    }

    private static async Task AssertNotFound(Result<PageSourceVersionSnapshot?> result)
    {
        var failure = result as Result<PageSourceVersionSnapshot?>.Failure;
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Error).IsTypeOf<AeroError.NotFound>();
    }
}
