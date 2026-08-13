using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;
using TUnit.Core;

namespace Aero.Cms.E2E.Tests;

/// <summary>Real-browser coverage for generic localization and query-backed entry references.</summary>
[NotInParallel]
public sealed class LocalizationContentEntryE2ETests
{
    private const string Alias = "e2e-localized-entry";
    private static PlaywrightE2EFixture Fixture => SharedPlaywrightE2EFixture.Instance;
    private static LocalizationContentEntrySeed? Seed;

    [Before(TestSession)]
    public static async Task SetupSessionAsync()
    {
        await Fixture.InitializeAsync();
        Seed = await Fixture.SeedLocalizedContentEntryAsync(Alias);
    }

    [After(TestSession)]
    public static Task TeardownSessionAsync() => Fixture.DisposeAsync().AsTask();

    [Test]
    public async Task LocalizedEntryShowsFieldModesReferencePreviewAndCanonicalRtlPreview()
    {
        await Fixture.RunBrowserJourneyAsync(async () =>
        {
            await Fixture.LoginAsync();
            await Fixture.WarmUpBlazorAsync();
            var page = Fixture.Page!;
            var seed = Seed!;
            var consoleErrors = new List<string>();
            var failedRequests = new List<string>();
            page.Console += (_, message) =>
            {
                if (message.Type is "error" or "warning") consoleErrors.Add($"{message.Type}: {message.Text}");
            };
            page.RequestFailed += (_, request) => failedRequests.Add($"{request.Method} {request.Url}: {request.Failure}");

            await page.GotoAsync($"{Fixture.BaseUrl}/manager/content/{seed.Alias}/editor/{seed.ItemId}", new()
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30_000
            });

            await Assertions.Expect(page.GetByText("Shared code", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Localized name", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Fork note", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Shared", new() { Exact = true }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Localized", new() { Exact = true }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Copy on fork", new() { Exact = true })).ToBeVisibleAsync();

            await Assertions.Expect(page.GetByText("Only the provider and stable entry ID are saved.", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Selected entry preview", Exact = true, Level = 4 })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Sample entry", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("fixture", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Preview values are for confirmation only and are never copied into this field.", new() { Exact = true })).ToBeVisibleAsync();

            var itemResponse = await page.APIRequest.GetAsync(
                $"{Fixture.BaseUrl}/api/v1/admin/content-items/{seed.Alias}/{seed.ItemId}");
            itemResponse.Status.ShouldBe(200);
            var itemJson = await itemResponse.TextAsync();
            itemJson.ShouldContain("e2e-entry");
            itemJson.ShouldNotContain("Sample entry");
            itemJson.ShouldNotContain("\"kind\":\"fixture\"");

            await page.GetByRole(AriaRole.Button, new() { Name = "Preview published page", Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator(".pe-preview-url-bar-mini code")).ToHaveTextAsync("/ar-sa/e2e-localized-entry/rtl-localized-reference");
            var preview = page.Locator(".pe-preview-device-viewport");
            (await preview.GetAttributeAsync("lang")).ShouldBe("ar-SA");
            (await preview.GetAttributeAsync("dir")).ShouldBe("rtl");

            consoleErrors.ShouldBeEmpty();
            failedRequests.ShouldBeEmpty();
        });
    }

    [Test]
    public async Task LocalizedEntryRemainsAccessibleAndFitsA390PixelViewport()
    {
        await Fixture.RunBrowserJourneyAsync(async () =>
        {
            await Fixture.LoginAsync();
            await Fixture.WarmUpBlazorAsync();
            var page = Fixture.Page!;
            var seed = Seed!;
            await page.SetViewportSizeAsync(390, 844);
            try
            {
                await page.GotoAsync($"{Fixture.BaseUrl}/manager/content/{seed.Alias}/editor/{seed.ItemId}", new()
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30_000
                });

                var fields = page.GetByRole(AriaRole.Region, new() { Name = "Entry fields", Exact = true });
                await Assertions.Expect(fields).ToBeVisibleAsync();
                await Assertions.Expect(page.GetByLabel("Related entry preview value", new() { Exact = true })).ToBeVisibleAsync();
                var metricsJson = await page.EvaluateAsync<string>("() => JSON.stringify({ viewport: innerWidth, scrollWidth: document.documentElement.scrollWidth, bodyWidth: document.body.scrollWidth })");
                using var metrics = JsonDocument.Parse(metricsJson);
                metrics.RootElement.GetProperty("viewport").GetInt32().ShouldBe(390);
                metrics.RootElement.GetProperty("scrollWidth").GetInt32().ShouldBeLessThanOrEqualTo(390);
                metrics.RootElement.GetProperty("bodyWidth").GetInt32().ShouldBeLessThanOrEqualTo(390);
            }
            finally
            {
                await page.SetViewportSizeAsync(1280, 720);
            }
        });
    }

    [Test]
    public async Task AiAssistedTranslationWithoutReviewMetadataFailsClosedInTheManagerAndApi()
    {
        await Fixture.RunBrowserJourneyAsync(async () =>
        {
            await Fixture.LoginAsync();
            await Fixture.WarmUpBlazorAsync();
            var page = Fixture.Page!;
            var seed = Seed!;

            await page.GotoAsync($"{Fixture.BaseUrl}/manager/content/{seed.Alias}/editor/{seed.AiBlockedItemId}?tab=translations", new()
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30_000
            });

            await Assertions.Expect(page.GetByText("Review metadata unavailable", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("The current manager contract does not expose translation provenance or revision-bound review data. Publication remains subject to server-side policy; this screen does not infer approval.", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true })).ToBeDisabledAsync();

            var response = await page.APIRequest.PostAsync(
                $"{Fixture.BaseUrl}/api/v1/admin/content-items/{seed.Alias}/{seed.AiBlockedItemId}/publish");
            response.Status.ShouldBe(400);
        });
    }
}
