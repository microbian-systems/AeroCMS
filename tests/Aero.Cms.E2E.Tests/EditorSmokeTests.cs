using FluentAssertions;
using Microsoft.Playwright;
using TUnit.Core;

namespace Aero.Cms.E2E.Tests;

[NotInParallel]
public sealed class EditorSmokeTests
{
    private static readonly PlaywrightE2EFixture Fixture = new();

    [Before(TestSession)]
    public static Task SetupSessionAsync() => Fixture.InitializeAsync();

    [After(TestSession)]
    public static Task TeardownSessionAsync() => Fixture.DisposeAsync().AsTask();

    [Test]
    public async Task AuthApiResponds()
    {
        await Fixture.LoginAsync();
        var page = Fixture.Page!;

        var response = await page.APIRequest.GetAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/auth/me");

        response.Status.Should().Be(200);
    }

    [Test]
    public async Task PagesGridShowsSeededPage()
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var page = Fixture.Page!;

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/pages", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        try
        {
            await page.GetByText("Home", new() { Exact = true }).First.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });
        }
        catch (TimeoutException exception)
        {
            var bodyText = await page.Locator("body").InnerTextAsync();
            var apiResponse = await page.APIRequest.GetAsync(
                $"{Fixture.BaseUrl}/api/v1/admin/pages/tree/translation-groups/children?culture=en-US");
            var apiBody = await apiResponse.TextAsync();
            var cookies = await Fixture.BrowserContext!.CookiesAsync(Fixture.BaseUrl);
            var cookieSummary = string.Join(
                ", ",
                cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"));
            throw new InvalidOperationException(
                $"Seeded page was not rendered at {page.Url}. " +
                $"Tree API returned {apiResponse.Status}: {apiBody}. " +
                $"Cookies: {cookieSummary}.{Environment.NewLine}{bodyText}",
                exception);
        }
    }

    [Test]
    public async Task EditorRendersLivingStandardShell()
    {
        var page = await OpenNewEditorAsync();

        await page.Locator(".pe-page-header").WaitForAsync(Visible());
        await page.Locator(".pe-tabs").WaitForAsync(Visible());
        await page.Locator(".aero-page-canvas__surface").WaitForAsync(Visible());
        await page.Locator(".aero-page-canvas__empty").WaitForAsync(Visible());

        await OpenPaletteAsync(page);
        await page.Locator(".aero-element-palette").WaitForAsync(Visible());
    }

    [Test]
    public async Task ElementPaletteFiltersManifestElements()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.Locator("#aero-element-search").FillAsync("button");

        var items = page.Locator("[data-aero-palette-kind='element']");
        await items.First.WaitForAsync(Visible());
        var tags = await items.EvaluateAllAsync<string[]>(
            "elements => elements.map(element => element.getAttribute('data-aero-palette-value'))");

        tags.Should().Equal("button");
    }

    [Test]
    public async Task ClickInsertionSupportsUndoAndRedo()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='section']").ClickAsync();
        await WaitForNodeCountAsync(page, 1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true }).ClickAsync();
        await WaitForNodeCountAsync(page, 0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Redo", Exact = true }).ClickAsync();
        await WaitForNodeCountAsync(page, 1);
    }

    [Test]
    public async Task LayoutCanBePointerDraggedOntoEmptyCanvas()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        var source = page.Locator(
            "[data-aero-palette-kind='layout'][data-aero-palette-value='OneColumn']");
        var target = page.Locator(".aero-page-canvas__surface");
        var sourceBox = await source.BoundingBoxAsync();
        var targetBox = await target.BoundingBoxAsync();
        sourceBox.Should().NotBeNull();
        targetBox.Should().NotBeNull();

        await page.Mouse.MoveAsync(
            sourceBox!.X + sourceBox.Width / 2,
            sourceBox.Y + sourceBox.Height / 2);
        await page.Mouse.DownAsync();
        await Task.Delay(150);
        await page.Mouse.MoveAsync(
            targetBox!.X + targetBox.Width / 2,
            targetBox.Y + targetBox.Height / 2,
            new() { Steps = 12 });
        await page.Mouse.UpAsync();

        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('[data-aero-node-id]').length > 0",
            null,
            new() { Timeout = 10_000 });
    }

    [Test]
    public async Task MetadataAndTranslationsTabsRemainAvailable()
    {
        var page = await OpenNewEditorAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Metadata", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Page Metadata" }).WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Culture / Translations", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Culture / Translations" }).WaitForAsync(Visible());
    }

    private static async Task<IPage> OpenNewEditorAsync()
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var page = Fixture.Page!;

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/page/editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        await page.Locator(".aero-page-canvas__surface").WaitForAsync(Visible());
        return page;
    }

    private static async Task OpenPaletteAsync(IPage page)
    {
        var sidebar = page.Locator(".pe-sidebar-right");
        await sidebar.WaitForAsync(new() { Timeout = 10_000 });
        var className = await sidebar.GetAttributeAsync("class") ?? string.Empty;
        if (className.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("collapsed"))
        {
            await sidebar.Locator(".pe-collapse-btn").ClickAsync();
        }

        await page.Locator(".aero-element-palette").WaitForAsync(Visible());
    }

    private static Task WaitForNodeCountAsync(IPage page, int expected) =>
        page.WaitForFunctionAsync(
            $"() => document.querySelectorAll('[data-aero-node-id]').length === {expected}",
            null,
            new() { Timeout = 10_000 });

    private static LocatorWaitForOptions Visible() => new()
    {
        State = WaitForSelectorState.Visible,
        Timeout = 10_000
    };
}
