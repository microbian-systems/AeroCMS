using Microsoft.Playwright;
using Shouldly;
using TUnit.Core;
using System.Text.Json;

namespace Aero.Cms.E2E.Tests;

/// <summary>Real-browser coverage for the additive query-backed content-type editor tab.</summary>
[NotInParallel]
public sealed class SurrealViewEditorE2ETests
{
    private const string ContentTypeAlias = "e2e-surreal-view";
    private static PlaywrightE2EFixture Fixture => SharedPlaywrightE2EFixture.Instance;

    [Before(TestSession)]
    public static async Task SetupSessionAsync()
    {
        await Fixture.InitializeAsync();
        await Fixture.SeedSurrealViewContentTypeAsync(ContentTypeAlias);
    }

    [After(TestSession)]
    public static Task TeardownSessionAsync() => Fixture.DisposeAsync().AsTask();

    [Test]
    public async Task ExistingContentTypeCanPreviewSaveAndPublishASurrealView()
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var page = Fixture.Page!;

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/content-type/editor/{ContentTypeAlias}", new()
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        // The new tab is additive: the original editor tabs remain available.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Basics", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Fields", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Display", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Surreal View", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Surreal View", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("The default is 300 seconds (5 minutes).", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Duration (seconds)", new() { Exact = true })).ToHaveValueAsync("300");

        const string listQuery = "SELECT id, title, kind FROM e2e_records WHERE tenant_id = $tenantId AND site_id = $siteId LIMIT 20";
        const string exactQuery = "SELECT id, title, kind FROM e2e_records WHERE tenant_id = $tenantId AND site_id = $siteId AND id = $entryId LIMIT 1";
        const string searchQuery = "SELECT id, title, kind FROM e2e_records WHERE tenant_id = $tenantId AND site_id = $siteId AND title CONTAINS $search LIMIT 20";

        await FillMonacoAsync(page, "SurrealQL SELECT query", listQuery);
        await page.GetByRole(AriaRole.Button, new() { Name = "Run preview", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Discovered output", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("e2e-entry", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("id", new() { Exact = true }).Last).ToBeVisibleAsync();

        await page.Locator("label").Filter(new() { HasText = "Identity field" }).Locator("select").SelectOptionAsync("id");
        await page.Locator("label").Filter(new() { HasText = "Title field (optional)" }).Locator("select").SelectOptionAsync("title");
        await FillMonacoAsync(page, "SurrealQL exact virtual entry SELECT query", exactQuery);
        await FillMonacoAsync(page, "SurrealQL virtual entry search SELECT query", searchQuery);

        await page.GetByRole(AriaRole.Button, new() { Name = "Save draft", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Draft saved. Preview and publish this exact revision when it is ready.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Saved revision is eligible for public execution.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Publish view", Exact = true })).ToBeEnabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Relationships", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No managed or database-owned relationships were found for this shape.", new() { Exact = true })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Publish view", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Published this immutable view revision.", new() { Exact = true })).ToBeVisibleAsync();

        var invalidated = await page.APIRequest.PostAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/content-views/{ContentTypeAlias}/cache/invalidate");
        invalidated.Status.ShouldBe(200);

        var known = await page.APIRequest.GetAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/content-views/entries/view%3A{ContentTypeAlias}/e2e-entry");
        known.Status.ShouldBe(200);
        (await known.TextAsync()).ShouldContain("e2e-entry");

        // A provider-qualified missing entry must be a 404, never a null/empty 200 response.
        var missing = await page.APIRequest.GetAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/content-views/entries/view%3A{ContentTypeAlias}/missing-entry");
        missing.Status.ShouldBe(404);

    }

    [Test]
    public async Task SurrealViewEditorFitsMobileViewportWithoutPageOverflow()
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var page = Fixture.Page!;
        await page.SetViewportSizeAsync(390, 844);

        try
        {
            await page.GotoAsync($"{Fixture.BaseUrl}/manager/content-type/editor/{ContentTypeAlias}", new()
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30_000
            });

            var menu = page.Locator(".pe-mobile-sidebar-toggle");
            await Assertions.Expect(menu).ToBeVisibleAsync();
            (await menu.GetAttributeAsync("aria-label")).ShouldBe("Open manager menu");
            await menu.ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dismiss manager menu", Exact = true })).ToBeVisibleAsync();
            (await menu.GetAttributeAsync("aria-expanded")).ShouldBe("true");
            await page.Locator(".pe-mobile-sidebar-backdrop").ClickAsync(new LocatorClickOptions
            {
                Position = new Position { X = 350, Y = 100 }
            });
            (await menu.GetAttributeAsync("aria-label")).ShouldBe("Open manager menu");
            (await menu.GetAttributeAsync("aria-expanded")).ShouldBe("false");

            await page.GetByRole(AriaRole.Button, new() { Name = "Surreal View", Exact = true }).ClickAsync();
            var metricsJson = await page.EvaluateAsync<string>(
                @"() => {
                    const editor = document.querySelector('.pe-editor-area');
                    const tabs = document.querySelector('.content-type-editor__tabs');
                    const editorRect = editor?.getBoundingClientRect();
                    return JSON.stringify({
                        viewport: window.innerWidth,
                        scrollWidth: document.documentElement.scrollWidth,
                        bodyWidth: document.body.scrollWidth,
                        editorLeft: Math.round(editorRect?.left ?? -1),
                        editorWidth: Math.round(editorRect?.width ?? 0),
                        tabsClientWidth: tabs?.clientWidth ?? 0,
                        tabsScrollWidth: tabs?.scrollWidth ?? 0
                    });
                }");
            using var metricsDocument = JsonDocument.Parse(metricsJson);
            var metrics = metricsDocument.RootElement;

            metrics.GetProperty("viewport").GetInt32().ShouldBe(390);
            metrics.GetProperty("scrollWidth").GetInt32().ShouldBeLessThanOrEqualTo(metrics.GetProperty("viewport").GetInt32());
            metrics.GetProperty("bodyWidth").GetInt32().ShouldBeLessThanOrEqualTo(metrics.GetProperty("viewport").GetInt32());
            metrics.GetProperty("editorLeft").GetInt32().ShouldBe(0);
            metrics.GetProperty("editorWidth").GetInt32().ShouldBe(metrics.GetProperty("viewport").GetInt32());
            metrics.GetProperty("tabsScrollWidth").GetInt32().ShouldBeGreaterThan(metrics.GetProperty("tabsClientWidth").GetInt32());
        }
        finally
        {
            await page.SetViewportSizeAsync(1280, 720);
        }
    }

    private static async Task FillMonacoAsync(IPage page, string accessibleLabel, string value)
    {
        var editor = page.GetByLabel(accessibleLabel, new() { Exact = true });
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await editor.ClickAsync(new LocatorClickOptions { Force = true });
        await editor.PressAsync("ControlOrMeta+A");
        await editor.PressSequentiallyAsync(value);
        await editor.PressAsync("ControlOrMeta+End");
    }
}
