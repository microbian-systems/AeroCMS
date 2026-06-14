using Aero.Cms.Shared.Pages.Manager.PageEditor.Palette;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class PageEditorPaletteSearchTests
{
    [Test]
    public async Task EmptyQueryHidesClearControlAndNoResultsMessage()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            [nameof(PageEditorPaletteSearch.Query)] = string.Empty,
            [nameof(PageEditorPaletteSearch.HasResults)] = true
        });

        html.Should().Contain("data-testid=\"palette-search-input\"");
        html.Should().NotContain("data-testid=\"palette-search-clear\"");
        html.Should().NotContain("data-testid=\"palette-search-empty\"");
    }

    [Test]
    public async Task ActiveQueryShowsClearControl()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            [nameof(PageEditorPaletteSearch.Query)] = "hero",
            [nameof(PageEditorPaletteSearch.HasResults)] = true,
            [nameof(PageEditorPaletteSearch.ClearLabel)] = "Clear palette search"
        });

        html.Should().Contain("value=\"hero\"");
        html.Should().Contain("data-testid=\"palette-search-clear\"");
        html.Should().Contain("aria-label=\"Clear palette search\"");
    }

    [Test]
    public async Task NoResultsRendersAccessibleStatus()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            [nameof(PageEditorPaletteSearch.Query)] = "not-a-block",
            [nameof(PageEditorPaletteSearch.HasResults)] = false,
            [nameof(PageEditorPaletteSearch.NoResultsText)] = "Nothing found"
        });

        html.Should().Contain("data-testid=\"palette-search-empty\"");
        html.Should().Contain("role=\"status\"");
        html.Should().Contain("Nothing found");
    }

    private static async Task<string> RenderAsync(
        IDictionary<string, object?> parameters)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadzenComponents();

        await using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PageEditorPaletteSearch>(
                ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
