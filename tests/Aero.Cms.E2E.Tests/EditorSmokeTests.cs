using FluentAssertions;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;
using TUnit.Core;

namespace Aero.Cms.E2E.Tests;

[NotInParallel]
public sealed class EditorSmokeTests
{
    private static PlaywrightE2EFixture Fixture => SharedPlaywrightE2EFixture.Instance;

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
    public async Task MarkdownTiptapNormalizationPreservesTrailingCodeBlankLines()
    {
        await Fixture.LoginAsync();
        var page = Fixture.Page!;

        var normalized = await page.EvaluateAsync<string>(
            """
            async () => {
                const module = await import(
                    '/_content/Aero.Cms.Shared/js/aero-tiptap-markdown-editor.js');
                return module.normalizeMarkdownHtml(
                    '<pre><code>line\r\n\r\n</code></pre>');
            }
            """);

        normalized.Should().Be("<pre><code>line\n\n</code></pre>");
    }

    [Test]
    public async Task MarkdownTiptapNormalizationRemovesEmptyParagraphsAndCanonicalizesTables()
    {
        await Fixture.LoginAsync();
        var page = Fixture.Page!;

        var normalized = await page.EvaluateAsync<string>(
            """
            async () => {
                const module = await import(
                    '/_content/Aero.Cms.Shared/js/aero-tiptap-markdown-editor.js');
                return module.normalizeMarkdownHtml(
                    '<p><br></p><table style="min-width: 200px"><colgroup><col></colgroup><tbody><tr><th colspan="1"><p>Feature</p></th><th rowspan="1"><p>Status</p></th></tr><tr><td><p>Images</p></td><td><p>Ready</p></td></tr></tbody></table>');
            }
            """);

        normalized.Should().Be(
            "<table><thead><tr><th>Feature</th><th>Status</th></tr></thead><tbody><tr><td>Images</td><td>Ready</td></tr></tbody></table>");
    }

    [Test]
    public async Task MarkdownTiptapNormalizationMovesBoundaryWhitespaceOutsideInlineMarks()
    {
        await Fixture.LoginAsync();
        var page = Fixture.Page!;

        var normalized = await page.EvaluateAsync<string[]>(
            """
            async () => {
                const module = await import(
                    '/_content/Aero.Cms.Shared/js/aero-tiptap-markdown-editor.js');
                return [
                    module.normalizeMarkdownHtml(
                        '<p>Before<strong>  bold text \t</strong>after</p>'),
                    module.normalizeMarkdownHtml(
                        '<p><strong><em>  nested text  </em></strong></p>'),
                    module.normalizeMarkdownHtml(
                        '<p>Before<del> \t </del>after</p>'),
                    module.normalizeMarkdownHtml(
                        '<p><strong> <img src="/media/example.jpg" alt="Example"> </strong></p>'),
                    module.normalizeMarkdownHtml(
                        '<p><strong data-preserve="true"> attributed text </strong></p>')
                ];
            }
            """);

        normalized.Should().Equal(
            "<p>Before  <strong>bold text</strong> \tafter</p>",
            "<p>  <strong><em>nested text</em></strong>  </p>",
            "<p>Before \t after</p>",
            "<p> <strong><img src=\"/media/example.jpg\" alt=\"Example\"></strong> </p>",
            "<p><strong data-preserve=\"true\"> attributed text </strong></p>");
    }

    [Test]
    public async Task MarkdownTiptapPreservesExistingImageAttributes()
    {
        await Fixture.LoginAsync();
        var page = Fixture.Page!;

        var imageHtml = await page.EvaluateAsync<string>(
            """
            async () => {
                const module = await import(
                    '/_content/Aero.Cms.Shared/js/aero-tiptap-markdown-editor.js');
                const host = document.createElement('div');
                document.body.appendChild(host);
                const handle = await module.initialize(
                    host,
                    '<p><img src="/media/example.jpg" alt="Example" title="A title"></p>');
                try {
                    return module.getHtml(handle);
                } finally {
                    module.dispose(handle);
                    host.remove();
                }
            }
            """);

        imageHtml.Should().Contain("src=\"/media/example.jpg\"");
        imageHtml.Should().Contain("alt=\"Example\"");
        imageHtml.Should().Contain("title=\"A title\"");
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
    public async Task ComponentPaletteExpandsAndSearchesCuratedTemplates()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        var components = page.Locator("[data-aero-palette-kind='component']");
        var basicComponents = page.Locator("[data-aero-palette-kind='component'][data-aero-palette-category='Basics']");
        var daisyComponents = page.Locator("[data-aero-palette-kind='component'][data-aero-palette-category='Daisy']");
        (await basicComponents.CountAsync()).Should().Be(6);
        (await daisyComponents.CountAsync()).Should().Be(15);
        (await page.Locator("[data-aero-palette-value='basic.feature-comparison']").CountAsync()).Should().Be(0);

        await page.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new Regex("^Show all \\d+ Basics$")
        })
            .ClickAsync();
        await page.Locator("[data-aero-palette-value='basic.feature-comparison']").WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Daisy", Exact = true }).ClickAsync();
        (await components.CountAsync()).Should().Be(15);
        (await basicComponents.CountAsync()).Should().Be(0);
        await page.Locator("[data-aero-palette-value='daisy.accordion']").WaitForAsync(Visible());

        await page.Locator("#aero-element-search").FillAsync("accordion");
        (await components.CountAsync()).Should().Be(2);
        var daisyAccordion = page.Locator("[data-aero-palette-kind='component'][data-aero-palette-value='daisy.accordion']");
        await daisyAccordion.ClickAsync();
        await page.Locator(".aero-page-canvas__surface details").WaitForAsync(Visible());
        await page.Locator(".aero-element-palette__guidance")
            .GetByText("Showing 2 matching options.", new() { Exact = true })
            .WaitForAsync(Visible());
    }

    [Test]
    public async Task ThemeStudioRendersScopedResponsivePreviewWithoutAssigningSite()
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var page = Fixture.Page!;

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/sites/{Fixture.SiteId}/theme-studio", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        await page.GetByRole(AriaRole.Heading, new() { Name = "Theme Studio" }).WaitForAsync(Visible());
        await page.GetByRole(AriaRole.Button, new() { Name = "Light" }).WaitForAsync(Visible());
        (await page.Locator("[data-theme='theme-studio-light'] .d-btn").CountAsync()).Should().BeGreaterThan(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Patterns" }).ClickAsync();
        await page.GetByText("Marketing hero", new() { Exact = true }).WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Phone preview" }).ClickAsync();
        await page.Locator(".preview-frame--phone").WaitForAsync(Visible());
    }

    [Test]
    public async Task PatternPaletteFiltersAndInsertsAResponsiveAccessibleFeatureComposition()
    {
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var page = await OpenBlankDraftEditorAsync(
            $"Pattern palette {suffix}",
            $"pattern-palette-{suffix}");
        await OpenPaletteAsync(page);

        var patternsFilter = page.GetByRole(AriaRole.Button, new() { Name = "Patterns", Exact = true });
        await patternsFilter.ClickAsync();
        await Assertions.Expect(patternsFilter).ToHaveAttributeAsync("aria-pressed", "true");

        var patterns = page.Locator("[data-aero-palette-kind='component'][data-aero-palette-category='Patterns']");
        await Assertions.Expect(patterns).ToHaveCountAsync(4);
        await Assertions.Expect(page.Locator("[data-aero-palette-value='pattern.marketing-hero-actions']"))
            .ToHaveAttributeAsync("data-aero-palette-label", "Marketing hero + actions");
        await Assertions.Expect(page.Locator("[data-aero-palette-value='pattern.feature-card-grid']"))
            .ToHaveAttributeAsync("data-aero-palette-label", "Feature card grid");
        await Assertions.Expect(page.Locator("[data-aero-palette-value='pattern.call-to-action-banner']"))
            .ToHaveAttributeAsync("data-aero-palette-label", "Call-to-action banner");
        await Assertions.Expect(page.Locator("[data-aero-palette-value='pattern.product-card']"))
            .ToHaveAttributeAsync("data-aero-palette-label", "Product card");
        var featurePreview = page.Locator("img[data-aero-pattern-preview-for='pattern.feature-card-grid']");
        await Assertions.Expect(featurePreview).ToHaveAttributeAsync(
            "src",
            "/_content/Aero.Cms.Shared/images/page-builder/pattern-previews/feature-card-grid.svg");
        await Assertions.Expect(featurePreview).ToHaveAttributeAsync("alt", string.Empty);
        await page.Locator("[data-aero-palette-value='pattern.feature-card-grid']")
            .GetByText("Pattern · Content", new() { Exact = true })
            .WaitForAsync(Visible());

        await DragPaletteItemOntoEmptyCanvasAsync(page, "component", "pattern.feature-card-grid");
        var responsiveGrid = page.Locator(".aero-page-canvas__surface ul[class~='md:grid-cols-3']");
        await responsiveGrid.WaitForAsync(Visible());
        await page.Locator(".aero-page-canvas__surface")
            .GetByRole(AriaRole.Link, new() { Name = "Explore planning", Exact = true })
            .WaitForAsync(Visible());
    }

    [Test]
    public async Task TypedContentPalettePagesItemsAndEditsListQueryWithUndoRedo()
    {
        var alias = $"page-editor-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        await Fixture.SeedContentPaletteAsync(alias, itemCount: 15);
        var page = await OpenNewEditorAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Content", Exact = true }).ClickAsync();
        var palette = page.Locator(".aero-content-type-palette");
        await palette.WaitForAsync(Visible());
        await palette.Locator("label").Filter(new() { HasText = "Content type" })
            .Locator("select").SelectOptionAsync(alias);

        await Assertions.Expect(palette.Locator(".aero-content-type-palette__pager"))
            .ToContainTextAsync("15 total");
        await Assertions.Expect(palette.Locator("label").Filter(new() { HasText = "Content item" })
            .Locator("option")).ToHaveCountAsync(10);

        await palette.Locator(".aero-content-type-palette__pager")
            .GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Assertions.Expect(palette.Locator(".aero-content-type-palette__pager"))
            .ToContainTextAsync("Page 2 of 2");
        await Assertions.Expect(palette.Locator("label").Filter(new() { HasText = "Content item" })
            .Locator("option")).ToHaveCountAsync(5);

        await palette.Locator("[data-aero-palette-kind='contentlist']").ClickAsync();
        var listScope = page.Locator(".aero-page-canvas__surface > section[data-aero-node-id]");
        await Assertions.Expect(listScope).ToHaveCountAsync(1);
        await Assertions.Expect(listScope.Locator(":scope > article[data-aero-node-id]"))
            .ToHaveCountAsync(1);

        await page.GetByRole(AriaRole.Tab, new() { Name = "Inspector", Exact = true }).ClickAsync();
        var queryEditor = page.Locator(".aero-content-query");
        await queryEditor.WaitForAsync(Visible());
        var pageSize = queryEditor.Locator("label").Filter(new() { HasText = "Page size" }).Locator("input");
        await pageSize.FillAsync("25");
        await queryEditor.Locator("label").Filter(new() { HasText = "Sort field" })
            .Locator("select").SelectOptionAsync("headline");
        await queryEditor.GetByRole(AriaRole.Button, new() { Name = "Add filter", Exact = true }).ClickAsync();
        var filter = queryEditor.Locator(".aero-content-query__filter");
        await filter.Locator("select").Nth(0).SelectOptionAsync("score");
        await filter.Locator("select").Nth(1).SelectOptionAsync("GreaterThanOrEqual");
        await filter.Locator("input").FillAsync("5");
        await queryEditor.GetByRole(AriaRole.Button, new() { Name = "Apply list settings", Exact = true })
            .ClickAsync();
        await page.GetByText("Content list settings updated.", new() { Exact = true }).WaitForAsync(Visible());
        await Assertions.Expect(pageSize).ToHaveValueAsync("25");

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true }).ClickAsync();
        await Assertions.Expect(pageSize).ToHaveValueAsync("10");
        await page.GetByRole(AriaRole.Button, new() { Name = "Redo", Exact = true }).ClickAsync();
        await Assertions.Expect(pageSize).ToHaveValueAsync("25");
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
    public async Task PaletteDoubleClickInsertsExactlyOneElement()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='section']").DblClickAsync();
        await WaitForNodeCountAsync(page, 1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true }).ClickAsync();
        await WaitForNodeCountAsync(page, 0);
    }

    [Test]
    public async Task StaticHtmlFragmentImportsAsOneUndoableEditorMutation()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.GetByRole(AriaRole.Button, new() { Name = "Import HTML", Exact = true }).ClickAsync();
        var dialog = page.Locator(".aero-fragment-import-dialog");
        await dialog.WaitForAsync(Visible());
        await dialog.Locator("textarea").FillAsync(
            "<section><h2>Imported heading</h2><p>Imported copy.</p></section>");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Import", Exact = true }).ClickAsync();

        await dialog.WaitForAsync(new()
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });
        await page.GetByText("Imported heading", new() { Exact = true }).WaitForAsync(Visible());
        await WaitForNodeCountAsync(page, 3);

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true }).ClickAsync();
        await WaitForNodeCountAsync(page, 0);
    }

    [Test]
    public async Task CustomHtmlFragmentMonacoExpandsAppliesAndRetainsSource()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);
        var uniqueHeading =
            $"Custom HTML Monaco {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var source =
            $"<section><h2>{uniqueHeading}</h2><p>Source survives resize and reopen.</p></section>";

        await page.Locator(
                "[data-aero-palette-kind='renderedfragment'][data-aero-palette-value='CustomHtml']")
            .ClickAsync();

        const string dialogSelector = ".aero-custom-html-dialog";
        var dialog = page.Locator(dialogSelector);
        await dialog.WaitForAsync(Visible());
        var monacoEditor = dialog.Locator(".monaco-editor");
        await monacoEditor.WaitForAsync(Visible());
        await Assertions.Expect(monacoEditor).ToHaveCountAsync(1);
        await SetDialogMonacoValueAsync(page, dialogSelector, source);

        var expansionButton = dialog.Locator(".aero-monaco-source-editor__expand");
        await Assertions.Expect(expansionButton)
            .ToHaveAttributeAsync("aria-pressed", "false");
        await expansionButton.ClickAsync();
        await Assertions.Expect(expansionButton)
            .ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(dialog)
            .ToHaveClassAsync(new Regex("aero-custom-html-dialog--expanded"));
        (await GetDialogMonacoValueAsync(page, dialogSelector)).Should().Be(source);

        await expansionButton.ClickAsync();
        await Assertions.Expect(expansionButton)
            .ToHaveAttributeAsync("aria-pressed", "false");
        await Assertions.Expect(dialog)
            .ToHaveClassAsync(new Regex("^aero-custom-html-dialog$"));
        (await GetDialogMonacoValueAsync(page, dialogSelector)).Should().Be(source);

        await dialog.GetByRole(AriaRole.Button, new()
        {
            Name = "Apply HTML",
            Exact = true
        }).ClickAsync();
        await dialog.WaitForAsync(new()
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();
        var preview = page.Locator("iframe[title='Page preview']");
        await preview.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await page.FrameLocator("iframe[title='Page preview']")
            .GetByRole(AriaRole.Heading, new()
            {
                Name = uniqueHeading,
                Exact = true
            })
            .WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });

        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Close Preview",
            Exact = true
        }).ClickAsync();
        await preview.WaitForAsync(new()
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });

        var fragment = page.Locator(
            ".aero-page-canvas__surface > section[data-aero-node-id]");
        await Assertions.Expect(fragment).ToHaveCountAsync(1);
        await fragment.EvaluateAsync(
            "element => element.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }))");

        dialog = page.Locator(dialogSelector);
        await dialog.WaitForAsync(Visible());
        await dialog.Locator(".monaco-editor").WaitForAsync(Visible());
        (await GetDialogMonacoValueAsync(page, dialogSelector)).Should().Be(source);
    }

    [Test]
    public async Task PreviewDeviceControlsApplyDistinctViewportWidths()
    {
        var page = await OpenNewEditorAsync();

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();

        var viewport = page.Locator(".pe-preview-device-viewport");
        await viewport.WaitForAsync(Visible());
        await Assertions.Expect(viewport)
            .ToHaveAttributeAsync("data-preview-device", "desktop");

        var desktopWidth = (await viewport.BoundingBoxAsync())!.Width;

        var tabletButton = page.GetByRole(AriaRole.Button, new()
        {
            Name = "Preview at tablet width",
            Exact = true
        });
        await tabletButton.ClickAsync();
        await Assertions.Expect(tabletButton)
            .ToHaveAttributeAsync("aria-pressed", "true");
        await page.WaitForFunctionAsync(
            "() => Math.abs(document.querySelector('.pe-preview-device-viewport')?.getBoundingClientRect().width - 768) < 1");
        var tabletWidth = (await viewport.BoundingBoxAsync())!.Width;

        var mobileButton = page.GetByRole(AriaRole.Button, new()
        {
            Name = "Preview at mobile width",
            Exact = true
        });
        await mobileButton.ClickAsync();
        await Assertions.Expect(mobileButton)
            .ToHaveAttributeAsync("aria-pressed", "true");
        await page.WaitForFunctionAsync(
            "() => Math.abs(document.querySelector('.pe-preview-device-viewport')?.getBoundingClientRect().width - 375) < 1");
        var mobileWidth = (await viewport.BoundingBoxAsync())!.Width;

        desktopWidth.Should().BeGreaterThan(tabletWidth);
        tabletWidth.Should().BeApproximately(768, 1);
        mobileWidth.Should().BeApproximately(375, 1);
        tabletWidth.Should().BeGreaterThan(mobileWidth);

        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Close Preview",
            Exact = true
        }).ClickAsync();
    }

    [Test]
    public async Task FullPageScribanSourceSurvivesSurfaceChangesPreviewSaveAndReload()
    {
        var page = await OpenNewEditorAsync("aero.scriban");
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var title = $"Scriban Source {suffix}";
        var renderedText = $"Source preview {suffix}";
        var source = $"<main><h1>{{{{ page.title }}}}</h1><p>{renderedText}</p></main>";

        const string workspaceSelector = ".pe-source-workspace";
        var workspace = page.Locator(workspaceSelector);
        await workspace.WaitForAsync(Visible());
        await workspace.Locator(".monaco-editor").WaitForAsync(Visible());
        await SetDialogMonacoValueAsync(page, workspaceSelector, source);
        await page.Locator(".pe-page-title-input:visible").Last.FillAsync(title);

        var expansionButton = workspace.GetByRole(AriaRole.Button, new()
        {
            Name = "Expand Scriban editor",
            Exact = true
        });
        await expansionButton.ClickAsync();
        await Assertions.Expect(workspace)
            .ToHaveClassAsync(new Regex("pe-source-workspace--expanded"));
        (await GetDialogMonacoValueAsync(page, workspaceSelector)).Should().Be(source);
        await workspace.GetByRole(AriaRole.Button, new()
        {
            Name = "Restore Scriban editor",
            Exact = true
        }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Metadata", Exact = true })
            .ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Content Editor", Exact = true })
            .ClickAsync();
        await workspace.Locator(".monaco-editor").WaitForAsync(Visible());
        (await GetDialogMonacoValueAsync(page, workspaceSelector)).Should().Be(source);

        await page.GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();
        var preview = page.Locator("iframe[title='Page preview']");
        await preview.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await page.FrameLocator("iframe[title='Page preview']")
            .GetByText(renderedText, new() { Exact = true })
            .WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Close Preview",
            Exact = true
        }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })
            .ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/manager/page/editor/\d+$"), new()
        {
            Timeout = 30_000
        });
        await page.GetByText("Page created successfully", new() { Exact = true })
            .WaitForAsync(Visible());

        var pageId = long.Parse(page.Url[(page.Url.LastIndexOf('/') + 1)..]);
        var sourceResponse = await page.APIRequest.GetAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/pages/{pageId}/source");
        var sourceBody = await sourceResponse.TextAsync();
        sourceResponse.Status.Should().Be(200, sourceBody);
        using (var sourceDocument = JsonDocument.Parse(sourceBody))
        {
            sourceDocument.RootElement.GetProperty("source").GetString().Should().Be(source);
        }

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        workspace = page.Locator(workspaceSelector);
        await workspace.Locator(".monaco-editor").WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        (await GetDialogMonacoValueAsync(page, workspaceSelector)).Should().Be(source);
    }

    [Test]
    public async Task FeatureComparisonTableRendersAsCompactSemanticTableOnMobilePreview()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await AddComponentFromPaletteAsync(page, "basic.feature-comparison", "Feature comparison");
        var table = page.Locator(".aero-page-canvas__surface table");
        await table.WaitForAsync(Visible());
        (await table.Locator("caption").CountAsync()).Should().Be(1);
        (await table.Locator("thead th[scope='col']").CountAsync()).Should().Be(3);
        (await table.Locator("tbody tr").CountAsync()).Should().Be(4);

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();
        var iframe = page.Locator("iframe[title='Page preview']");
        await iframe.WaitForAsync(Visible());
        await page.Locator("button[title='Mobile']").ClickAsync();
        await page.WaitForTimeoutAsync(350);

        await AssertNoHorizontalOverflowAsync(page.FrameLocator("iframe[title='Page preview']"));
    }

    [Test]
    public async Task LayoutCanBePointerDraggedOntoEmptyCanvas()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await DragPaletteItemOntoEmptyCanvasAsync(page, "layout", "OneColumn");

        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('[data-aero-node-id]').length > 0",
            null,
            new() { Timeout = 10_000 });
    }

    [Test]
    public async Task NestedCompositionSupportsPointerKeyboardUndoRedoAndReload()
    {
        var page = await OpenNewEditorAsync();
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var title = $"Nested Composition {suffix}";

        await OpenPaletteAsync(page);
        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='section']")
            .ClickAsync();

        var section = page.Locator(
            ".aero-page-canvas__surface > section[data-aero-node-id]");
        await section.WaitForAsync(Visible());

        await ReturnToPaletteAsync(page);
        await DragPaletteItemInsideNodeAsync(page, "h2", section);
        await ReturnToPaletteAsync(page);
        await DragPaletteItemInsideNodeAsync(page, "p", section);

        var paragraph = section.Locator(":scope > p[data-aero-node-id]");
        await paragraph.DblClickAsync();
        var editor = page.Locator(".aero-tiptap-prosemirror");
        await editor.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await editor.FillAsync("Nested content persists");
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply text", Exact = true }).ClickAsync();
        await page.Locator(".aero-rich-text-dialog").WaitForAsync(new()
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });

        var heading = section.Locator(":scope > h2[data-aero-node-id]");
        await paragraph.ClickAsync();
        await DragSelectedNodeRelativeAsync(page, heading, HtmlDropPosition.Before);
        (await DirectChildTagsAsync(section)).Should().Equal("p", "h2");

        await PressSelectedMoveKeyAsync(page, "ArrowLeft");
        await Assertions.Expect(page.Locator(
            ".aero-page-canvas__surface > p[data-aero-node-id]"))
            .ToHaveCountAsync(1);
        (await DirectChildTagsAsync(section)).Should().Equal("h2");

        await PressSelectedMoveKeyAsync(page, "ArrowRight");
        await Assertions.Expect(section.Locator(":scope > p[data-aero-node-id]"))
            .ToHaveCountAsync(1);
        (await DirectChildTagsAsync(section)).Should().Equal("h2", "p");

        await PressSelectedMoveKeyAsync(page, "ArrowLeft");
        await Assertions.Expect(page.Locator(
            ".aero-page-canvas__surface > p[data-aero-node-id]"))
            .ToHaveCountAsync(1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true }).ClickAsync();
        await Assertions.Expect(section.Locator(":scope > p[data-aero-node-id]"))
            .ToHaveCountAsync(1);
        await page.GetByRole(AriaRole.Button, new() { Name = "Redo", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(
            ".aero-page-canvas__surface > p[data-aero-node-id]"))
            .ToHaveCountAsync(1);

        await PressSelectedMoveKeyAsync(page, "ArrowRight");
        await Assertions.Expect(section.Locator(":scope > p[data-aero-node-id]"))
            .ToHaveCountAsync(1);
        await page.Locator(".pe-page-title-input:visible").Last.FillAsync(title);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/manager/page/editor/\d+$"), new()
        {
            Timeout = 30_000
        });
        await page.GetByText("Page created successfully", new() { Exact = true }).WaitForAsync(Visible());

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        await page.Locator(".aero-page-canvas__surface").WaitForAsync(Visible());
        section = page.Locator(".aero-page-canvas__surface > section[data-aero-node-id]");
        await Assertions.Expect(section).ToHaveCountAsync(1);
        (await DirectChildTagsAsync(section)).Should().Equal("h2", "p");
        await section.GetByText("Nested content persists", new() { Exact = true })
            .WaitForAsync(Visible());
    }

    [Test]
    public async Task DocumentOutlineSelectsNestedNodesAndTheirBreadcrumbParents()
    {
        var page = await OpenNewEditorAsync();

        await OpenPaletteAsync(page);
        await DragPaletteItemOntoEmptyCanvasAsync(page, "component", "basic.hero");

        var outline = page.Locator(".aero-page-outline");
        await outline.GetByRole(AriaRole.Heading, new() { Name = "Document outline", Exact = true })
            .WaitForAsync(Visible());

        var headingEntry = outline.GetByRole(AriaRole.Button, new()
        {
            Name = "<h1> Build something remarkable",
            Exact = true
        });
        await headingEntry.ClickAsync();

        await Assertions.Expect(page.Locator(".aero-property-panel__header h2"))
            .ToContainTextAsync("Heading 1");
        await Assertions.Expect(page.Locator("h1[data-aero-node-id]").Filter(new()
        {
            HasText = "Build something remarkable"
        })).ToHaveClassAsync(new Regex("aero-editor-node-selected"));

        var breadcrumbs = outline.Locator(".aero-page-outline__breadcrumbs");
        await Assertions.Expect(breadcrumbs).ToContainTextAsync("section");
        await breadcrumbs.GetByRole(AriaRole.Button, new() { Name = "<section>", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".aero-property-panel__header h2"))
            .ToContainTextAsync("Section");
        await Assertions.Expect(page.Locator(".aero-page-canvas__surface > section[data-aero-node-id]"))
            .ToHaveClassAsync(new Regex("aero-editor-node-selected"));
    }

    [Test]
    public async Task KeyboardCommandsPreserveFocusAnnounceChangesAndRejectInvalidNesting()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);
        await page.Locator("#aero-element-search").FillAsync("span");
        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='span']")
            .ClickAsync();
        await ReturnToPaletteAsync(page);
        await page.Locator("#aero-element-search").FillAsync("section");
        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='section']")
            .ClickAsync();

        var surface = page.Locator(".aero-page-canvas__surface");
        var sections = surface.Locator(":scope > section[data-aero-node-id]");
        await Assertions.Expect(sections).ToHaveCountAsync(1);

        var undo = page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true });
        var duplicate = page.GetByRole(AriaRole.Button, new() { Name = "Duplicate element", Exact = true });
        var delete = page.GetByRole(AriaRole.Button, new() { Name = "Delete element", Exact = true });
        (await undo.GetAttributeAsync("aria-keyshortcuts")).Should().Contain("Control+Z");
        (await duplicate.GetAttributeAsync("aria-keyshortcuts")).Should().Contain("Control+D");
        (await delete.GetAttributeAsync("aria-keyshortcuts")).Should().Contain("Delete");

        var titleInput = page.Locator(".pe-page-title-input:visible").Last;
        await titleInput.FocusAsync();
        await titleInput.PressAsync("Control+D");
        await Assertions.Expect(sections).ToHaveCountAsync(1);

        await sections.First.ClickAsync();
        var handle = page.Locator(".aero-page-canvas__drag-handle.is-visible");
        await handle.FocusAsync();
        await handle.PressAsync("Control+D");
        await Assertions.Expect(sections).ToHaveCountAsync(2);
        await page.GetByText("Element duplicated.", new() { Exact = true }).WaitForAsync(Visible());
        await Assertions.Expect(handle).ToBeFocusedAsync();

        await handle.PressAsync("Delete");
        await Assertions.Expect(sections).ToHaveCountAsync(1);
        await page.GetByText("Element removed.", new() { Exact = true }).WaitForAsync(Visible());
        await Assertions.Expect(surface.Locator(".aero-editor-node-selected")).ToHaveCountAsync(1);
        await Assertions.Expect(handle).ToBeFocusedAsync();

        await handle.PressAsync("Control+Z");
        await Assertions.Expect(sections).ToHaveCountAsync(2);
        await page.GetByText("Change undone.", new() { Exact = true }).WaitForAsync(Visible());
        await Assertions.Expect(handle).ToBeFocusedAsync();

        await handle.PressAsync("Control+Y");
        await Assertions.Expect(sections).ToHaveCountAsync(1);
        await page.GetByText("Change redone.", new() { Exact = true }).WaitForAsync(Visible());
        await Assertions.Expect(handle).ToBeFocusedAsync();

        await handle.PressAsync("ArrowRight");
        await page.GetByRole(AriaRole.Alert)
            .GetByText(new Regex("span.*section|section.*span", RegexOptions.IgnoreCase))
            .WaitForAsync(Visible());
        await Assertions.Expect(sections).ToHaveCountAsync(1);
        await Assertions.Expect(surface.Locator(":scope > span > section")).ToHaveCountAsync(0);
        await Assertions.Expect(handle).ToBeFocusedAsync();

        await handle.PressAsync("Delete");
        await Assertions.Expect(sections).ToHaveCountAsync(0);
        await Assertions.Expect(surface.Locator(":scope > span.aero-editor-node-selected"))
            .ToHaveCountAsync(1);
        await Assertions.Expect(handle).ToBeFocusedAsync();

        await handle.PressAsync("Delete");
        await Assertions.Expect(surface.Locator(":scope > [data-aero-node-id]"))
            .ToHaveCountAsync(0);
        await Assertions.Expect(surface).ToBeFocusedAsync();
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

    [Test]
    public async Task RichTextEditorUpdatesLivingStandardParagraph()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);
        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='p']").ClickAsync();

        var paragraph = page.Locator("[data-aero-node-id]").Filter(new()
        {
            HasText = "Start writing here..."
        });
        await paragraph.DblClickAsync();

        var editor = page.Locator(".aero-tiptap-prosemirror");
        await editor.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await editor.FillAsync("Edited with Aero rich text");
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply text", Exact = true }).ClickAsync();

        await page.GetByText("Edited with Aero rich text", new() { Exact = true }).WaitForAsync(Visible());
        await page.Locator(".aero-rich-text-dialog").WaitForAsync(new()
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });
    }

    [Test]
    public async Task RichTextStrikeAndCodeApplyAsOneUndoableChange()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);
        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='p']").ClickAsync();

        var paragraph = page.Locator("[data-aero-node-id]").Filter(new()
        {
            HasText = "Start writing here..."
        });
        await paragraph.DblClickAsync();

        var editor = page.Locator(".aero-tiptap-prosemirror");
        await editor.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await editor.FillAsync("Outdated Aero.Run()");

        var strike = page.GetByRole(AriaRole.Button, new() { Name = "Strikethrough", Exact = true });
        var code = page.GetByRole(AriaRole.Button, new() { Name = "Inline code", Exact = true });
        await SelectEditorTextAsync(page, editor, "Outdated");
        await strike.ClickAsync();
        await Assertions.Expect(strike).ToHaveAttributeAsync("aria-pressed", "true");
        await SelectEditorTextAsync(page, editor, "Aero.Run()");
        await code.ClickAsync();
        await Assertions.Expect(code).ToHaveAttributeAsync("aria-pressed", "true");

        await page.GetByRole(AriaRole.Button, new() { Name = "Apply text", Exact = true }).ClickAsync();
        await page.Locator(".aero-rich-text-dialog").WaitForAsync(new()
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });

        paragraph = page.Locator("p[data-aero-node-id]").Filter(new()
        {
            HasText = "Outdated Aero.Run()"
        });
        await Assertions.Expect(paragraph.Locator("s")).ToHaveTextAsync("Outdated");
        await Assertions.Expect(paragraph.Locator("code")).ToHaveTextAsync("Aero.Run()");

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("p[data-aero-node-id] s")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("p[data-aero-node-id] code")).ToHaveCountAsync(0);
        await page.GetByText("Start writing here...", new() { Exact = true }).WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Redo", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("p[data-aero-node-id] s")).ToHaveTextAsync("Outdated");
        await Assertions.Expect(page.Locator("p[data-aero-node-id] code")).ToHaveTextAsync("Aero.Run()");
    }

    [Test]
    public async Task SplitHeroCanBeDraggedEditedSavedReloadedPublishedAndRendered()
    {
        var page = await OpenNewEditorAsync();
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var title = $"Split Hero {suffix}";
        var slug = $"split-hero-{suffix}";
        const string heading = "A better first impression from Aero";
        const string imageSource = "/media/updated-split-hero.jpg";
        const string alternativeText = "Aero team collaborating on a page";

        await OpenPaletteAsync(page);
        await DragPaletteItemOntoEmptyCanvasAsync(page, "component", "basic.split-hero");

        var heroHeading = page.Locator("h1[data-aero-node-id]").Filter(new()
        {
            HasText = "Make a stronger first impression"
        });
        await heroHeading.WaitForAsync(Visible());
        await heroHeading.DblClickAsync();

        var editor = page.Locator(".aero-tiptap-prosemirror");
        await editor.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await editor.FillAsync(heading);
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply text", Exact = true }).ClickAsync();
        await page.GetByText(heading, new() { Exact = true }).WaitForAsync(Visible());

        var heroImage = page.Locator("img[data-aero-node-id][alt='Describe the main hero image']");
        await heroImage.ClickAsync();
        await Assertions.Expect(heroImage).ToHaveClassAsync(new Regex("aero-editor-node-selected"));
        await Assertions.Expect(page.Locator(".aero-property-panel__header h2"))
            .ToContainTextAsync("Image");
        await page.GetByLabel("Image source").FillAsync(imageSource);
        await page.GetByLabel("Alternative text", new() { Exact = true }).FillAsync(alternativeText);
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply changes", Exact = true }).ClickAsync();
        await page.Locator($"img[data-aero-node-id][src='{imageSource}'][alt='{alternativeText}']")
            .WaitForAsync(Visible());

        await page.Locator(".pe-page-title-input:visible").Last.FillAsync(title);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/manager/page/editor/\d+$"), new()
        {
            Timeout = 30_000
        });
        await page.GetByText("Page created successfully", new() { Exact = true }).WaitForAsync(Visible());

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        await page.Locator(".aero-page-canvas__surface").WaitForAsync(Visible());
        await Assertions.Expect(page.Locator(".pe-page-title-input:visible").Last)
            .ToHaveValueAsync(title, new() { Timeout = 30_000 });
        await page.GetByText(heading, new() { Exact = true }).WaitForAsync(Visible());
        await page.Locator($"img[data-aero-node-id][src='{imageSource}'][alt='{alternativeText}']")
            .WaitForAsync(Visible());

        var persistedPath = await GetPersistedPagePathAsync(page);
        persistedPath.Should().Be($"/{slug}");

        await page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true }).ClickAsync();
        await page.GetByText("Page published!", new() { Exact = true }).WaitForAsync(Visible());

        using (var publishedDetail = await GetPersistedPageDetailAsync(page))
        {
            publishedDetail.RootElement.GetProperty("publicationState").GetInt32().Should().Be(1);
            publishedDetail.RootElement.GetProperty("publishedContent").ValueKind.Should().Be(JsonValueKind.Object);
        }

        var publicUrl = $"{Fixture.BaseUrl}{persistedPath}";
        string publicBody = string.Empty;
        var publicStatus = 0;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var publicResponse = await page.APIRequest.GetAsync(publicUrl);
            publicStatus = publicResponse.Status;
            publicBody = await publicResponse.TextAsync();
            if (publicStatus == 200
                && publicBody.Contains(heading, StringComparison.Ordinal)
                && publicBody.Contains(imageSource, StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(250);
        }

        publicStatus.Should().Be(200, publicBody);
        publicBody.Should().Contain(heading);
        publicBody.Should().Contain(imageSource);
        publicBody.Should().Contain(alternativeText);

        await page.GotoAsync(publicUrl, new()
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });
        await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true })
            .WaitForAsync(Visible());
        await page.Locator($"img[src='{imageSource}'][alt='{alternativeText}']").WaitForAsync(Visible());
    }

    [Test]
    public async Task DaisyAccordionCanBeDraggedSavedReloadedPreviewedPublishedAndRendered()
    {
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var title = $"Daisy Accordion {suffix}";
        var slug = $"daisy-accordion-{suffix}";
        var page = await OpenBlankDraftEditorAsync(title, slug);
        const string summaryText = "What is included?";
        const string contentText = "Everything in this component is ordinary editable HTML.";

        await OpenPaletteAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Daisy", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Daisy", Exact = true }))
            .ToHaveAttributeAsync("aria-pressed", "true");
        await DragPaletteItemOntoEmptyCanvasAsync(page, "component", "daisy.accordion");

        var accordion = page.Locator(".aero-page-canvas__surface details[data-aero-node-id]");
        await accordion.WaitForAsync(Visible());
        await accordion.Locator("summary").GetByText(summaryText, new() { Exact = true }).WaitForAsync(Visible());
        await accordion.GetByText(contentText, new() { Exact = true }).WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.GetByText("Page saved successfully", new() { Exact = true }).WaitForAsync(Visible());

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        accordion = page.Locator(".aero-page-canvas__surface details[data-aero-node-id]");
        await accordion.WaitForAsync(Visible());
        await accordion.Locator("summary").GetByText(summaryText, new() { Exact = true }).WaitForAsync(Visible());
        await accordion.GetByText(contentText, new() { Exact = true }).WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true }).ClickAsync();
        var preview = page.Locator("iframe[title='Page preview']");
        await preview.WaitForAsync(Visible());
        var previewFrame = page.FrameLocator("iframe[title='Page preview']");
        var previewAccordion = previewFrame.Locator("details");
        await previewAccordion.Locator("summary").GetByText(summaryText, new() { Exact = true }).WaitForAsync(Visible());
        await previewAccordion.GetByText(contentText, new() { Exact = true }).WaitForAsync(Visible());
        await page.GetByRole(AriaRole.Button, new() { Name = "Close Preview", Exact = true }).ClickAsync();

        var persistedPath = await GetPersistedPagePathAsync(page);
        persistedPath.Should().Be($"/{slug}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true }).ClickAsync();
        await page.GetByText("Page published!", new() { Exact = true }).WaitForAsync(Visible());

        var publicUrl = $"{Fixture.BaseUrl}{persistedPath}";
        string publicBody = string.Empty;
        var publicStatus = 0;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var publicResponse = await page.APIRequest.GetAsync(publicUrl);
            publicStatus = publicResponse.Status;
            publicBody = await publicResponse.TextAsync();
            if (publicStatus == 200 && publicBody.Contains(contentText, StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(250);
        }

        publicStatus.Should().Be(200, publicBody);
        publicBody.Should().Contain("<details");
        publicBody.Should().Contain(summaryText);
        publicBody.Should().Contain(contentText);

        await page.GotoAsync(publicUrl, new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        var publicAccordion = page.Locator("details");
        await publicAccordion.Locator("summary").GetByText(summaryText, new() { Exact = true }).WaitForAsync(Visible());
        await publicAccordion.GetByText(contentText, new() { Exact = true }).WaitForAsync(Visible());
    }

    [Test]
    public async Task VisualStylesPersistResponsivelyThroughReloadAndPublicRendering()
    {
        var page = await OpenNewEditorAsync();
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var title = $"Visual Styles {suffix}";
        var slug = $"visual-styles-{suffix}";
        const string headingText = "Framework-neutral visual styling";
        const string backgroundImage = "/_content/Aero.Cms.Shared/images/page-builder/hero.svg";

        await OpenPaletteAsync(page);
        await DragPaletteItemOntoEmptyCanvasAsync(page, "component", "basic.hero");

        var heading = page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Build something remarkable",
            Exact = true
        });
        await heading.DblClickAsync();
        var editor = page.Locator(".aero-tiptap-prosemirror");
        await editor.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await editor.FillAsync(headingText);
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply text", Exact = true }).ClickAsync();
        heading = page.GetByRole(AriaRole.Heading, new() { Name = headingText, Exact = true });
        await heading.WaitForAsync(Visible());

        var section = page.Locator(".aero-page-canvas__surface > section[data-aero-node-id]");
        await section.ClickAsync(new()
        {
            Position = new Position { X = 8, Y = 8 }
        });
        var panel = page.Locator(".aero-property-panel");
        await Assertions.Expect(panel.Locator("header h2")).ToContainTextAsync("Section");

        var layout = panel.Locator("details").Filter(new() { HasText = "Layout" }).First;
        await layout.GetByLabel("Display").SelectOptionAsync("Grid");
        await layout.GetByLabel("Columns").FillAsync("2");
        await layout.GetByLabel("Stack on mobile").CheckAsync();
        await layout.GetByLabel("Align items").SelectOptionAsync("Center");
        await layout.GetByLabel("Justify").SelectOptionAsync("Center");
        await SetLengthAsync(layout, "Gap", "1.5", "Rem");
        await SetLengthAsync(layout, "Minimum height", "60", "ViewportHeight");

        var spacing = panel.Locator("details").Filter(new() { HasText = "Spacing" }).First;
        await OpenDetailsAsync(spacing);
        var padding = spacing.Locator("fieldset").Filter(new() { HasText = "Padding" }).First;
        var margin = spacing.Locator("fieldset").Filter(new() { HasText = "Margin" }).First;
        await SetLengthAsync(padding, "Top", "3", "Rem");
        await SetLengthAsync(padding, "Left", "2", "Rem");
        await SetLengthAsync(margin, "Top", "1.5", "Rem");

        var surface = panel.Locator("details").Filter(new() { HasText = "Background & surface" }).First;
        await OpenDetailsAsync(surface);
        await surface.GetByLabel("Background color").FillAsync("#123456");
        var backgroundInput = surface.GetByLabel("Background image");
        await backgroundInput.FillAsync(backgroundImage);
        await backgroundInput.DispatchEventAsync("change");
        await surface.GetByLabel("Image fit").SelectOptionAsync("Cover");
        await surface.GetByLabel("Position").SelectOptionAsync("Center");
        await surface.GetByLabel("Repeat").SelectOptionAsync("NoRepeat");
        await surface.GetByLabel("Overlay color").FillAsync("#000000");
        await surface.GetByLabel("Overlay opacity").FillAsync("0.35");
        await SetLengthAsync(surface, "Corner radius", "1", "Rem");
        await panel.GetByRole(AriaRole.Button, new() { Name = "Apply changes", Exact = true }).ClickAsync();
        await page.GetByText("Element updated.", new() { Exact = true }).WaitForAsync(Visible());

        await page.SetViewportSizeAsync(1200, 900);
        (await GridColumnCountAsync(section)).Should().Be(2);
        var sectionStyle = await StyleSnapshotAsync(section);
        sectionStyle["display"].Should().Be("grid");
        sectionStyle["gap"].Should().Be("24px");
        sectionStyle["alignItems"].Should().Be("center");
        sectionStyle["justifyContent"].Should().Be("center");
        sectionStyle["paddingBlockStart"].Should().Be("48px");
        sectionStyle["paddingInlineStart"].Should().Be("32px");
        sectionStyle["marginBlockStart"].Should().Be("24px");
        sectionStyle["backgroundImage"].Should().Contain("linear-gradient").And.Contain("hero.svg");
        sectionStyle["backgroundSize"].Should().Be("cover, cover");
        sectionStyle["backgroundRepeat"].Should().Be("no-repeat, no-repeat");
        sectionStyle["borderRadius"].Should().Be("16px");

        await page.SetViewportSizeAsync(600, 900);
        (await GridColumnCountAsync(section)).Should().Be(1);
        await page.SetViewportSizeAsync(1440, 1000);

        await heading.ClickAsync();
        panel = page.Locator(".aero-property-panel");
        var typography = panel.Locator("details").Filter(new() { HasText = "Typography" }).First;
        await OpenDetailsAsync(typography);
        await SetLengthAsync(typography, "Font size", "3.25", "Rem");
        await typography.GetByLabel("Weight").FillAsync("700");
        await typography.GetByLabel("Line height").FillAsync("1.1");
        await typography.GetByLabel("Alignment").SelectOptionAsync("Center");
        await typography.GetByLabel("Use gradient text").CheckAsync();
        await typography.GetByLabel("Start color").FillAsync("#ff3366");
        await typography.GetByLabel("End color").FillAsync("#6633ff");
        await typography.GetByLabel("Gradient angle").FillAsync("135");
        await panel.GetByRole(AriaRole.Button, new() { Name = "Apply changes", Exact = true }).ClickAsync();

        var headingStyle = await StyleSnapshotAsync(heading);
        headingStyle["fontSize"].Should().Be("52px");
        headingStyle["fontWeight"].Should().Be("700");
        headingStyle["textAlign"].Should().Be("center");
        headingStyle["backgroundImage"].Should().Contain("linear-gradient");
        headingStyle["backgroundClip"].Should().Be("text");
        headingStyle["color"].Should().Be("rgba(0, 0, 0, 0)");

        await page.Locator(".pe-page-title-input:visible").Last.FillAsync(title);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/manager/page/editor/\d+$"), new()
        {
            Timeout = 30_000
        });
        await page.GetByText("Page created successfully", new() { Exact = true }).WaitForAsync(Visible());

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        await page.Locator(".aero-page-canvas__surface").WaitForAsync(Visible());
        heading = page.GetByRole(AriaRole.Heading, new() { Name = headingText, Exact = true });
        await heading.WaitForAsync(Visible());
        section = page.Locator(".aero-page-canvas__surface > section[data-aero-node-id]");
        await section.WaitForAsync(Visible());
        const string reloadedSectionSelector = ".aero-page-canvas__surface > section[data-aero-node-id]";
        await WaitForCanvasStyleAsync(page, reloadedSectionSelector, "grid");
        var reloadedSectionStyle = await StyleSnapshotAsync(page, reloadedSectionSelector);
        reloadedSectionStyle["display"].Should().Be("grid");
        (await GridColumnCountAsync(page, reloadedSectionSelector)).Should().Be(2);
        reloadedSectionStyle["backgroundImage"].Should().Contain("hero.svg");
        (await StyleSnapshotAsync(page, "h1[data-aero-node-id]"))["backgroundClip"].Should().Be("text");

        await page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true }).ClickAsync();
        await page.GetByText("Page published!", new() { Exact = true }).WaitForAsync(Visible());

        var publicUrl = $"{Fixture.BaseUrl}/{slug}";
        string publicBody = string.Empty;
        var publicStatus = 0;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var publicResponse = await page.APIRequest.GetAsync(publicUrl);
            publicStatus = publicResponse.Status;
            publicBody = await publicResponse.TextAsync();
            if (publicStatus == 200
                && publicBody.Contains(headingText, StringComparison.Ordinal)
                && publicBody.Contains(backgroundImage, StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(250);
        }

        publicStatus.Should().Be(200, publicBody);
        publicBody.Should().Contain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        publicBody.Should().Contain("@media (max-width: 48rem)");
        publicBody.Should().Contain("linear-gradient(135deg, #ff3366, #6633ff)");
        publicBody.Should().Contain(backgroundImage);

        await page.GotoAsync(publicUrl, new()
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });
        heading = page.GetByRole(AriaRole.Heading, new() { Name = headingText, Exact = true });
        await heading.WaitForAsync(Visible());
        section = heading.Locator("xpath=ancestor::section[1]");
        (await GridColumnCountAsync(section)).Should().Be(2);
        (await StyleSnapshotAsync(section))["backgroundImage"].Should().Contain("hero.svg");
        (await StyleSnapshotAsync(heading))["backgroundClip"].Should().Be("text");

        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(artifacts);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifacts, "visual-style-authoring-public.png"),
            FullPage = true
        });
    }

    [Test]
    public async Task CuratedComponentsRemainResponsiveInDesktopAndMobilePreview()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);
        await page.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new Regex("^Show all \\d+ Basics$")
        })
            .ClickAsync();

        await AddComponentFromPaletteAsync(page, "basic.split-hero", "Make a stronger first impression");
        await AddComponentFromPaletteAsync(page, "basic.feature-grid", "Everything you need");
        await AddComponentFromPaletteAsync(page, "basic.centered-call-to-action", "Turn interest into action");
        await AddComponentFromPaletteAsync(page, "basic.gallery", "Gallery");
        await AddComponentFromPaletteAsync(page, "basic.contact-form", "Let’s talk");

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();

        var iframe = page.Locator("iframe[title='Page preview']");
        await iframe.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        var frame = page.FrameLocator("iframe[title='Page preview']");
        await frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Make a stronger first impression",
            Exact = true
        }).WaitForAsync(Visible());

        var splitHero = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Make a stronger first impression",
            Exact = true
        }).Locator("xpath=ancestor::section[1]");
        var featureGrid = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Everything you need",
            Exact = true
        }).Locator("xpath=following-sibling::div[1]");
        var galleryGrid = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Gallery",
            Exact = true
        }).Locator("xpath=following-sibling::div[1]");
        var contactSection = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Let’s talk",
            Exact = true
        }).Locator("xpath=ancestor::section[1]");

        (await GridColumnCountAsync(splitHero)).Should().Be(2);
        (await GridColumnCountAsync(featureGrid)).Should().Be(3);
        (await GridColumnCountAsync(galleryGrid)).Should().Be(3);
        (await GridColumnCountAsync(contactSection)).Should().Be(2);
        await AssertNoHorizontalOverflowAsync(frame);

        var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(artifacts);
        await iframe.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifacts, "curated-components-desktop.png")
        });
        await galleryGrid.ScrollIntoViewIfNeededAsync();
        await iframe.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifacts, "curated-components-desktop-gallery.png")
        });
        await splitHero.ScrollIntoViewIfNeededAsync();

        await page.Locator("button[title='Mobile']").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.pe-preview-iframe')?.getBoundingClientRect().width <= 376",
            null,
            new() { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(350);

        (await GridColumnCountAsync(splitHero)).Should().Be(1);
        (await GridColumnCountAsync(featureGrid)).Should().Be(1);
        (await GridColumnCountAsync(galleryGrid)).Should().Be(1);
        (await GridColumnCountAsync(contactSection)).Should().Be(1);
        await AssertNoHorizontalOverflowAsync(frame);
        await iframe.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifacts, "curated-components-mobile.png")
        });
        await contactSection.ScrollIntoViewIfNeededAsync();
        await iframe.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifacts, "curated-components-mobile-contact.png")
        });
    }

    [Test]
    public async Task PricingComponentInsertsSemanticCardsAndStacksInMobilePreview()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.Locator("#aero-element-search").FillAsync("Pricing");
        var pricingPaletteItem = page.GetByText("Pricing", new() { Exact = true });
        await pricingPaletteItem.WaitForAsync(Visible());
        await pricingPaletteItem.ClickAsync();

        var pricingHeading = page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Plans for every stage",
            Exact = true
        });
        await pricingHeading.WaitForAsync(Visible());
        var pricingSection = pricingHeading.Locator("xpath=ancestor::section[1]");
        var pricingGrid = pricingSection.Locator(":scope > div[data-aero-node-id]");

        await Assertions.Expect(pricingGrid).ToHaveCountAsync(1);
        await Assertions.Expect(pricingGrid.Locator(":scope > article[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(pricingGrid.Locator("h3[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(pricingGrid.Locator("ul[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(pricingGrid.Locator("a[data-aero-node-id]"))
            .ToHaveCountAsync(3);

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();

        var iframe = page.Locator("iframe[title='Page preview']");
        await iframe.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        var frame = page.FrameLocator("iframe[title='Page preview']");
        var previewGrid = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Plans for every stage",
            Exact = true
        }).Locator("xpath=following-sibling::div[1]");

        (await GridColumnCountAsync(previewGrid)).Should().Be(3);
        await page.Locator("button[title='Mobile']").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.pe-preview-iframe')?.getBoundingClientRect().width <= 376",
            null,
            new() { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(350);

        (await GridColumnCountAsync(previewGrid)).Should().Be(1);
    }

    [Test]
    public async Task LatestArticlesComponentInsertsSemanticCardsAndStacksInMobilePreview()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.Locator("#aero-element-search").FillAsync("Latest articles");
        var articlesPaletteItem = page.GetByText("Latest articles", new() { Exact = true });
        await articlesPaletteItem.WaitForAsync(Visible());
        await articlesPaletteItem.ClickAsync();

        var articlesHeading = page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Latest articles",
            Exact = true
        });
        await articlesHeading.WaitForAsync(Visible());
        var articlesSection = articlesHeading.Locator("xpath=ancestor::section[1]");
        var articleGrid = articlesSection.Locator(":scope > div[data-aero-node-id]");

        await Assertions.Expect(articleGrid).ToHaveCountAsync(1);
        await Assertions.Expect(articleGrid.Locator(":scope > article[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(articleGrid.Locator("h3[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(articleGrid.Locator("a[data-aero-node-id]"))
            .ToHaveCountAsync(3);

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();

        var iframe = page.Locator("iframe[title='Page preview']");
        await iframe.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        var frame = page.FrameLocator("iframe[title='Page preview']");
        var previewGrid = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Latest articles",
            Exact = true
        }).Locator("xpath=following-sibling::div[1]");

        (await GridColumnCountAsync(previewGrid)).Should().Be(3);
        await page.Locator("button[title='Mobile']").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.pe-preview-iframe')?.getBoundingClientRect().width <= 376",
            null,
            new() { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(350);

        (await GridColumnCountAsync(previewGrid)).Should().Be(1);
    }

    [Test]
    public async Task ShowcaseCollectionComponentInsertsSemanticCardsAndStacksInMobilePreview()
    {
        var page = await OpenNewEditorAsync();
        await OpenPaletteAsync(page);

        await page.Locator("#aero-element-search").FillAsync("Collection");
        var collectionPaletteItem = page.GetByText("Collection", new() { Exact = true });
        await collectionPaletteItem.WaitForAsync(Visible());
        await collectionPaletteItem.ClickAsync();

        var collectionHeading = page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Explore the collection",
            Exact = true
        });
        await collectionHeading.WaitForAsync(Visible());
        var collectionSection = collectionHeading.Locator("xpath=ancestor::section[1]");
        var collectionGrid = collectionSection.Locator(":scope > ul[data-aero-node-id]");

        await Assertions.Expect(collectionGrid).ToHaveCountAsync(1);
        await Assertions.Expect(collectionGrid.Locator(":scope > li[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(collectionGrid.Locator(":scope > li[data-aero-node-id] > article[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(collectionGrid.Locator("figure[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(collectionGrid.Locator("img[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(collectionGrid.Locator("h3[data-aero-node-id] > a[data-aero-node-id]"))
            .ToHaveCountAsync(3);
        await Assertions.Expect(collectionGrid.Locator(":scope > li[data-aero-node-id] > article[data-aero-node-id] > a[data-aero-node-id]"))
            .ToHaveCountAsync(3);

        await page.Locator(".pe-living-toolbar")
            .GetByRole(AriaRole.Button, new() { Name = "Preview", Exact = true })
            .ClickAsync();

        var iframe = page.Locator("iframe[title='Page preview']");
        await iframe.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        var frame = page.FrameLocator("iframe[title='Page preview']");
        var previewGrid = frame.GetByRole(AriaRole.Heading, new()
        {
            Name = "Explore the collection",
            Exact = true
        }).Locator("xpath=ancestor::header[1]/following-sibling::ul[1]");

        (await GridColumnCountAsync(previewGrid)).Should().Be(3);
        await page.Locator("button[title='Mobile']").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.pe-preview-iframe')?.getBoundingClientRect().width <= 376",
            null,
            new() { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(350);

        (await GridColumnCountAsync(previewGrid)).Should().Be(1);
    }

    [Test]
    public async Task CreateSaveReloadPublishAndPublicRenderUsesLivingStandardContent()
    {
        var page = await OpenNewEditorAsync();
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var title = $"Living Standard {suffix}";
        var slug = $"living-standard-{suffix}";

        await OpenPaletteAsync(page);
        await page.Locator(
            "[data-aero-palette-kind='element'][data-aero-palette-value='p']").ClickAsync();
        await WaitForNodeCountAsync(page, 1);
        await page.Locator(".pe-page-title-input:visible").Last.FillAsync(title);

        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/manager/page/editor/\d+$"), new()
        {
            Timeout = 30_000
        });
        await page.GetByText("Page created successfully", new() { Exact = true }).WaitForAsync(Visible());

        var pageId = long.Parse(page.Url[(page.Url.LastIndexOf('/') + 1)..]);
        var savedResponse = await page.APIRequest.GetAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/pages/{pageId}");
        var savedBody = await savedResponse.TextAsync();
        savedResponse.Status.Should().Be(200, savedBody);
        using (var savedJson = JsonDocument.Parse(savedBody))
        {
            savedJson.RootElement.GetProperty("title").GetString().Should().Be(title);
        }

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        await page.Locator(".aero-page-canvas__surface").WaitForAsync(Visible());
        await Assertions.Expect(page.Locator(".pe-page-title-input:visible").Last)
            .ToHaveValueAsync(title, new() { Timeout = 30_000 });
        await WaitForNodeCountAsync(page, 1);
        await page.GetByText("Start writing here...", new() { Exact = true }).WaitForAsync(Visible());

        await page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true }).ClickAsync();
        await page.GetByText("Page published!", new() { Exact = true }).WaitForAsync(Visible());

        var publicUrl = $"{Fixture.BaseUrl}/{slug}";
        string publicBody = string.Empty;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var publicApiResponse = await page.APIRequest.GetAsync(publicUrl);
            publicBody = await publicApiResponse.TextAsync();
            if (publicApiResponse.Status == 200
                && publicBody.Contains("Start writing here...", StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(250);
        }

        publicBody.Should().Contain("Start writing here...");
        await page.GotoAsync(publicUrl, new()
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });
        await page.GetByText("Start writing here...", new() { Exact = true }).WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
    }

    private static async Task SelectEditorTextAsync(
        IPage page,
        ILocator editor,
        string text)
    {
        await editor.EvaluateAsync(
            """
            (element, text) => {
                const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
                let node = walker.nextNode();
                while (node) {
                    const value = node.textContent ?? '';
                    const start = value.indexOf(text);
                    if (start >= 0) {
                        const range = document.createRange();
                        range.setStart(node, start);
                        range.setEnd(node, start + text.length);
                        const selection = window.getSelection();
                        selection?.removeAllRanges();
                        selection?.addRange(range);
                        document.dispatchEvent(new Event('selectionchange'));
                        return;
                    }

                    node = walker.nextNode();
                }

                throw new Error(`Editor text '${text}' was not found.`);
            }
            """,
            text);
        await page.WaitForTimeoutAsync(75);
    }

    private static async Task<IPage> OpenNewEditorAsync(
        string rendererId = "aero.composition")
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var page = Fixture.Page!;

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/page/editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        await page.GetByText("Choose a page type", new() { Exact = true })
            .WaitForAsync(Visible());
        await Assertions.Expect(page.GetByLabel("Page type", new() { Exact = true }))
            .ToHaveValueAsync("aero.composition");
        await page.GetByLabel("Page type", new() { Exact = true })
            .SelectOptionAsync(rendererId);
        await page.GetByRole(AriaRole.Button, new()
            {
                Name = "Create page",
                Exact = true
            })
            .Last
            .ClickAsync();

        await Assertions.Expect(page.Locator(".pe-page-title-input:visible").Last)
            .ToHaveValueAsync("New Page");
        var editorSurface = string.Equals(
            rendererId,
            "aero.composition",
            StringComparison.Ordinal)
            ? ".aero-page-canvas__surface"
            : ".pe-source-workspace";
        await page.Locator(editorSurface).WaitForAsync(Visible());
        return page;
    }

    private static async Task<IPage> OpenBlankDraftEditorAsync(string title, string slug)
    {
        await Fixture.LoginAsync();
        await Fixture.WarmUpBlazorAsync();
        var pageId = await Fixture.CreateBlankDraftPageAsync(title, slug);
        var page = Fixture.Page!;

        await page.GotoAsync($"{Fixture.BaseUrl}/manager/page/editor/{pageId}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30_000
        });

        await Assertions.Expect(page.Locator(".pe-page-title-input:visible").Last)
            .ToHaveValueAsync(title);
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

    private static async Task DragPaletteItemOntoEmptyCanvasAsync(
        IPage page,
        string kind,
        string value)
    {
        var source = page.Locator(
            $"[data-aero-palette-kind='{kind}'][data-aero-palette-value='{value}']");
        await source.WaitForAsync(Visible());
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.aero-page-canvas__surface')?.dataset.aeroSortableInitialized === 'true'",
            null,
            new() { Timeout = 30_000 });

        const int pointerId = 17;
        await source.DispatchEventAsync("pointerdown", new Dictionary<string, object>
        {
            ["pointerId"] = pointerId,
            ["button"] = 0,
            ["clientX"] = 0,
            ["clientY"] = 0
        });
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.aero-page-canvas__surface')?.dataset.aeroCanAcceptSelectedInside === 'true'",
            null,
            new() { Timeout = 30_000 });
        await page.EvaluateAsync(
            """
            pointerId => {
                const target = document.querySelector('.aero-page-canvas__empty');
                if (!target) throw new Error('Empty canvas drop target was not found.');
                const rect = target.getBoundingClientRect();
                const init = {
                    bubbles: true,
                    pointerId,
                    button: 0,
                    clientX: rect.left + rect.width / 2,
                    clientY: rect.top + rect.height / 2
                };
                target.dispatchEvent(new PointerEvent('pointermove', init));
                target.dispatchEvent(new PointerEvent('pointerup', init));
            }
            """,
            pointerId);
    }

    private static async Task AddComponentFromPaletteAsync(
        IPage page,
        string component,
        string expectedText)
    {
        var backButton = page.GetByRole(AriaRole.Button, new()
        {
            Name = "Back to elements",
            Exact = true
        });
        if (await backButton.CountAsync() > 0)
        {
            await backButton.ClickAsync();
        }

        var source = page.Locator(
            $"[data-aero-palette-kind='component'][data-aero-palette-value='{component}']");
        if (await source.CountAsync() == 0)
        {
            await page.GetByRole(AriaRole.Button, new()
            {
                NameRegex = new Regex("^Show all \\d+ Basics$")
            }).ClickAsync();
        }

        await source.ClickAsync();
        await page.GetByText(expectedText, new() { Exact = true }).WaitForAsync(Visible());
    }

    private static async Task ReturnToPaletteAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Back to elements",
            Exact = true
        }).ClickAsync();
        await page.Locator(".aero-element-palette").WaitForAsync(Visible());
    }

    private static async Task DragPaletteItemInsideNodeAsync(
        IPage page,
        string tag,
        ILocator target)
    {
        var source = page.Locator(
            $"[data-aero-palette-kind='element'][data-aero-palette-value='{tag}']");
        await source.WaitForAsync(Visible());

        const int pointerId = 23;
        await source.DispatchEventAsync("pointerdown", new Dictionary<string, object>
        {
            ["pointerId"] = pointerId,
            ["button"] = 0,
            ["clientX"] = 0,
            ["clientY"] = 0
        });
        await Assertions.Expect(target)
            .ToHaveAttributeAsync("data-aero-can-accept-selected-inside", "true");
        await target.EvaluateAsync(
            """
            (element, pointerId) => {
                const rect = element.getBoundingClientRect();
                const init = {
                    bubbles: true,
                    pointerId,
                    button: 0,
                    clientX: rect.left + rect.width / 2,
                    clientY: rect.top + rect.height / 2
                };
                element.dispatchEvent(new PointerEvent('pointermove', init));
                element.dispatchEvent(new PointerEvent('pointerup', init));
            }
            """,
            pointerId);
    }

    private static async Task DragSelectedNodeRelativeAsync(
        IPage page,
        ILocator target,
        HtmlDropPosition position)
    {
        var handle = page.Locator(".aero-page-canvas__drag-handle.is-visible");
        await handle.WaitForAsync(Visible());
        await target.ScrollIntoViewIfNeededAsync();
        var handleBox = await handle.BoundingBoxAsync();
        var targetBox = await target.BoundingBoxAsync();
        handleBox.Should().NotBeNull();
        targetBox.Should().NotBeNull();

        var targetY = position switch
        {
            HtmlDropPosition.Before => targetBox!.Y + 2,
            HtmlDropPosition.After => targetBox!.Y + targetBox.Height - 2,
            _ => targetBox!.Y + targetBox.Height / 2
        };
        await page.Mouse.MoveAsync(
            handleBox!.X + handleBox.Width / 2,
            handleBox.Y + handleBox.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(
            targetBox.X + targetBox.Width / 2,
            targetY,
            new() { Steps = 8 });
        await Assertions.Expect(handle).ToHaveClassAsync(
            new Regex(@"\bis-dragging\b"));
        var expectedDropClass = position switch
        {
            HtmlDropPosition.Before => "aero-sort-drop-before",
            HtmlDropPosition.After => "aero-sort-drop-after",
            _ => "aero-sort-drop-inside"
        };
        await Assertions.Expect(target).ToHaveClassAsync(
            new Regex($@"\b{expectedDropClass}\b"));
        await page.Mouse.UpAsync();
    }

    private static async Task PressSelectedMoveKeyAsync(IPage page, string key)
    {
        var handle = page.Locator(".aero-page-canvas__drag-handle.is-visible");
        await handle.WaitForAsync(Visible());
        await handle.FocusAsync();
        await handle.PressAsync(key);
    }

    private static Task<string[]> DirectChildTagsAsync(ILocator parent) =>
        parent.Locator(":scope > [data-aero-node-id]").EvaluateAllAsync<string[]>(
            "elements => elements.map(element => element.tagName.toLowerCase())");

    private enum HtmlDropPosition
    {
        Before,
        After,
        Inside
    }

    private static async Task<int> GridColumnCountAsync(ILocator locator)
    {
        var columns = await locator.EvaluateAsync<string>(
            "element => getComputedStyle(element).gridTemplateColumns");
        return columns.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static async Task<int> GridColumnCountAsync(IPage page, string selector)
    {
        var columns = await page.EvaluateAsync<string>(
            "selector => getComputedStyle(document.querySelector(selector)).gridTemplateColumns",
            selector);
        return columns.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static Task WaitForCanvasStyleAsync(IPage page, string selector, string display) =>
        page.WaitForFunctionAsync(
            "([selector, display]) => { const element = document.querySelector(selector); return element?.isConnected && getComputedStyle(element).display === display; }",
            new[] { selector, display },
            new() { Timeout = 10_000 });

    private static async Task OpenDetailsAsync(ILocator details)
    {
        if (!await details.EvaluateAsync<bool>("element => element.open"))
        {
            await details.Locator("summary").ClickAsync();
        }
    }

    private static async Task SetLengthAsync(
        ILocator scope,
        string label,
        string value,
        string unit)
    {
        var editor = scope.Locator(".aero-length-editor")
            .Filter(new() { HasText = label })
            .First;
        await editor.GetByRole(AriaRole.Spinbutton).FillAsync(value);
        await editor.GetByRole(AriaRole.Combobox).SelectOptionAsync(unit);
    }

    private static async Task<Dictionary<string, string>> StyleSnapshotAsync(ILocator locator)
    {
        var json = await locator.EvaluateAsync<string>(
            """
            element => {
                const style = getComputedStyle(element);
                return JSON.stringify({
                    display: style.display,
                    gap: style.gap,
                    alignItems: style.alignItems,
                    justifyContent: style.justifyContent,
                    paddingBlockStart: style.paddingBlockStart,
                    paddingInlineStart: style.paddingInlineStart,
                    marginBlockStart: style.marginBlockStart,
                    backgroundImage: style.backgroundImage,
                    backgroundSize: style.backgroundSize,
                    backgroundRepeat: style.backgroundRepeat,
                    borderRadius: style.borderRadius,
                    fontSize: style.fontSize,
                    fontWeight: style.fontWeight,
                    textAlign: style.textAlign,
                    backgroundClip: style.backgroundClip,
                    color: style.color
                });
            }
            """);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("The browser returned an empty style snapshot.");
    }

    private static async Task<Dictionary<string, string>> StyleSnapshotAsync(IPage page, string selector)
    {
        var json = await page.EvaluateAsync<string>(
            """
            selector => {
                const element = document.querySelector(selector);
                if (!element) throw new Error(`No element matched ${selector}.`);
                const style = getComputedStyle(element);
                return JSON.stringify({
                    display: style.display,
                    gap: style.gap,
                    alignItems: style.alignItems,
                    justifyContent: style.justifyContent,
                    paddingBlockStart: style.paddingBlockStart,
                    paddingInlineStart: style.paddingInlineStart,
                    marginBlockStart: style.marginBlockStart,
                    backgroundImage: style.backgroundImage,
                    backgroundSize: style.backgroundSize,
                    backgroundRepeat: style.backgroundRepeat,
                    borderRadius: style.borderRadius,
                    fontSize: style.fontSize,
                    fontWeight: style.fontWeight,
                    textAlign: style.textAlign,
                    backgroundClip: style.backgroundClip,
                    color: style.color
                });
            }
            """,
            selector);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("The browser returned an empty style snapshot.");
    }

    private static async Task AssertNoHorizontalOverflowAsync(IFrameLocator frame)
    {
        var widths = await frame.Locator("html").EvaluateAsync<int[]>(
            "element => [element.scrollWidth, element.clientWidth]");
        widths.Should().HaveCount(2);
        widths[0].Should().BeLessThanOrEqualTo(widths[1] + 1);

        var overflowingElements = await frame.Locator("body *").EvaluateAllAsync<string[]>(
            """
            elements => {
                const viewportWidth = document.documentElement.clientWidth;
                return elements
                    .filter(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.left < -1
                            || rect.right > viewportWidth + 1
                            || element.scrollWidth > element.clientWidth + 1;
                    })
                    .slice(0, 10)
                    .map(element => `${element.tagName.toLowerCase()}.${element.className || ''}`);
            }
            """);
        overflowingElements.Should().BeEmpty();
    }

    private static async Task SetDialogMonacoValueAsync(
        IPage page,
        string dialogSelector,
        string source)
    {
        await WaitForDialogMonacoAsync(page, dialogSelector);
        await page.EvaluateAsync(
            """
            args => {
                const editor = globalThis.monaco.editor.getEditors()
                    .filter(candidate => {
                        const node = candidate.getDomNode();
                        return node?.isConnected && node.closest(args.dialogSelector);
                    });
                if (editor.length !== 1) {
                    throw new Error(
                        `Expected one live Monaco editor in ${args.dialogSelector}, found ${editor.length}.`);
                }

                editor[0].setValue(args.source);
            }
            """,
            new { dialogSelector, source });
    }

    private static async Task<string> GetDialogMonacoValueAsync(
        IPage page,
        string dialogSelector)
    {
        await WaitForDialogMonacoAsync(page, dialogSelector);
        return await page.EvaluateAsync<string>(
            """
            dialogSelector => {
                const editor = globalThis.monaco.editor.getEditors()
                    .filter(candidate => {
                        const node = candidate.getDomNode();
                        return node?.isConnected && node.closest(dialogSelector);
                    });
                if (editor.length !== 1) {
                    throw new Error(
                        `Expected one live Monaco editor in ${dialogSelector}, found ${editor.length}.`);
                }

                return editor[0].getValue();
            }
            """,
            dialogSelector);
    }

    private static Task WaitForDialogMonacoAsync(
        IPage page,
        string dialogSelector) =>
        page.WaitForFunctionAsync(
            """
            dialogSelector => Boolean(
                globalThis.monaco?.editor?.getEditors?.()
                    .filter(editor => {
                        const node = editor.getDomNode();
                        return node?.isConnected && node.closest(dialogSelector);
                    }).length === 1)
            """,
            dialogSelector,
            new() { Timeout = 30_000 });

    private static Task WaitForNodeCountAsync(IPage page, int expected) =>
        page.WaitForFunctionAsync(
            $"() => document.querySelectorAll('[data-aero-node-id]').length === {expected}",
            null,
            new() { Timeout = 10_000 });

    private static async Task<string> GetPersistedPagePathAsync(IPage page)
    {
        using var document = await GetPersistedPageDetailAsync(page);
        return document.RootElement.GetProperty("path").GetString()
            ?? throw new InvalidOperationException("The persisted page did not have a path.");
    }

    private static async Task<JsonDocument> GetPersistedPageDetailAsync(IPage page)
    {
        var match = Regex.Match(page.Url, @"/manager/page/editor/(?<id>\d+)$");
        match.Success.Should().BeTrue($"the editor URL should contain the persisted page ID: {page.Url}");

        var response = await page.APIRequest.GetAsync(
            $"{Fixture.BaseUrl}/api/v1/admin/pages/{match.Groups["id"].Value}");
        response.Status.Should().Be(200);

        return JsonDocument.Parse(await response.TextAsync());
    }

    private static LocatorWaitForOptions Visible() => new()
    {
        State = WaitForSelectorState.Visible,
        Timeout = 10_000
    };
}
