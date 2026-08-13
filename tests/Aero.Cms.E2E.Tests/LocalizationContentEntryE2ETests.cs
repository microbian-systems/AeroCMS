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

            await NavigateToEditorAsync(page, seed);

            var consoleErrors = new List<string>();
            var failedRequests = new List<string>();
            page.Console += (_, message) =>
            {
                if (message.Type is "error" or "warning") consoleErrors.Add($"{message.Type}: {message.Text}");
            };
            page.RequestFailed += (_, request) => failedRequests.Add($"{request.Method} {request.Url}: {request.Failure}");

            await Assertions.Expect(FieldLabel(page, "Shared code")).ToBeVisibleAsync();
            await Assertions.Expect(FieldLabel(page, "Localized name")).ToBeVisibleAsync();
            await Assertions.Expect(FieldLabel(page, "Fork note")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Shared", new() { Exact = true }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Localized", new() { Exact = true }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Copy on fork", new() { Exact = true })).ToBeVisibleAsync();

            await Assertions.Expect(page.GetByText("Only the provider and stable entry ID are saved.", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Selected entry preview", Exact = true, Level = 4 })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByLabel("title preview value", new() { Exact = true })).ToHaveTextAsync("Sample entry");
            await Assertions.Expect(page.GetByLabel("kind preview value", new() { Exact = true })).ToHaveTextAsync("fixture");
            await Assertions.Expect(page.GetByText("Preview values are for confirmation only and are never copied into this field.", new() { Exact = true })).ToBeVisibleAsync();

            var itemResponse = await page.APIRequest.GetAsync(
                $"{Fixture.BaseUrl}/api/v1/admin/content-items/{seed.Alias}/{seed.ItemId}");
            itemResponse.Status.ShouldBe(200);
            var itemJson = await itemResponse.TextAsync();
            itemJson.ShouldContain("e2e-entry");
            itemJson.ShouldNotContain("Sample entry");
            itemJson.ShouldNotContain("\"kind\":\"fixture\"");

            var publicPreviewResponse = page.WaitForResponseAsync(response =>
                response.Url.EndsWith(
                    "/ar-SA/e2e-localized-entry/rtl-localized-reference",
                    StringComparison.Ordinal));
            await page.GetByRole(AriaRole.Button, new() { Name = "Preview published page", Exact = true }).ClickAsync();
            (await publicPreviewResponse).Status.ShouldBe(200);
            await Assertions.Expect(page.Locator(".pe-preview-url-bar-mini code")).ToHaveTextAsync("/ar-SA/e2e-localized-entry/rtl-localized-reference");
            var preview = page.Locator(".pe-preview-device-viewport");
            (await preview.GetAttributeAsync("lang")).ShouldBe("ar-SA");
            (await preview.GetAttributeAsync("dir")).ShouldBe("rtl");

            var blockedPreviewScripts = consoleErrors
                .Where(IsExpectedSandboxScriptBlock)
                .ToArray();
            blockedPreviewScripts.ShouldNotBeEmpty();
            consoleErrors.Except(blockedPreviewScripts).ShouldBeEmpty();
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
                await NavigateToEditorAsync(page, seed);

                var fields = page.GetByRole(AriaRole.Region, new() { Name = "Entry fields", Exact = true });
                await Assertions.Expect(fields).ToBeVisibleAsync();
                await Assertions.Expect(page.GetByLabel("title preview value", new() { Exact = true })).ToHaveTextAsync("Sample entry");
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

    private static bool IsExpectedSandboxScriptBlock(string message) =>
        message.Contains("Blocked script execution", StringComparison.Ordinal) &&
        message.Contains("document's frame is sandboxed", StringComparison.Ordinal) &&
        message.Contains("'allow-scripts' permission is not set", StringComparison.Ordinal);

    private static async Task NavigateToEditorAsync(IPage page, LocalizationContentEntrySeed seed)
    {
        var contentTypeResponse = page.WaitForResponseAsync(response =>
            response.Status == 200
            && response.Url.EndsWith($"/api/v1/admin/content-types/{seed.Alias}", StringComparison.Ordinal));
        var contentItemResponse = page.WaitForResponseAsync(response =>
            response.Status == 200
            && response.Url.EndsWith($"/api/v1/admin/content-items/{seed.Alias}/{seed.ItemId}", StringComparison.Ordinal));

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/content/{seed.Alias}/editor/{seed.ItemId}", new()
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30_000
        });

        await Task.WhenAll(contentTypeResponse, contentItemResponse);
    }

    private static ILocator FieldLabel(IPage page, string label) =>
        page.Locator("label.pe-property-label").Filter(new() { HasTextString = label });
}
