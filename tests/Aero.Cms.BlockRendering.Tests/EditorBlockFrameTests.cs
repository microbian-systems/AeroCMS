using Aero.Cms.Shared.Pages.Manager.PageEditor;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Radzen;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class EditorBlockFrameTests
{
    [Test]
    public async Task RendersContextMenuSuppressionHook()
    {
        var html = await RenderAsync();

        html.Should().Contain("__internal_preventDefault_oncontextmenu");
    }

    [Test]
    public async Task SelectedFrameRendersCanvasActions()
    {
        var html = await RenderAsync(isSelected: true);

        html.Should().Contain("pe-block-toolbar");
        html.Should().Contain("pe-toolbar-btn");
        html.Should().Contain("pe-toolbar-btn delete");
        html.Should().Contain("disabled");
    }

    private static async Task<string> RenderAsync(bool isSelected = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadzenComponents();
        services.AddSingleton(Substitute.For<IStringLocalizer<Shared.Localization.ManagerResource>>());

        await using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(EditorBlockFrame.BlockEditorId)] = "block-1",
                [nameof(EditorBlockFrame.Index)] = 0,
                [nameof(EditorBlockFrame.TotalCount)] = 2,
                [nameof(EditorBlockFrame.IsSelected)] = isSelected,
                [nameof(EditorBlockFrame.ChildContent)] =
                    (RenderFragment)(builder => builder.AddContent(0, "Preview"))
            });

            var output = await renderer.RenderComponentAsync<EditorBlockFrame>(parameters);
            return output.ToHtmlString();
        });
    }
}
