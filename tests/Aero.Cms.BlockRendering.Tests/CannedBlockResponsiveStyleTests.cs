using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class CannedBlockResponsiveStyleTests
{
    private readonly IEditorBlockMapper _mapper = new EditorBlockMapper(
        new PageEditorDefinitionRegistry(
            [new LegacyPageEditorBlockProvider()],
            []));

    [Test]
    public void MapperPreservesResponsiveStyleOnCannedBlock()
    {
        var editorBlock = new EditorBlock
        {
            Type = "boring_hero",
            MainText = "Styled hero",
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Padding = Uniform(24),
                    BackgroundColor = new CssColor("#112233")
                },
                Mobile = new NodeStyleOverride
                {
                    Padding = UniformOverride(8),
                    Direction = ContentDirection.RightToLeft
                }
            }
        };

        var mapped = _mapper.MapBlock(editorBlock)
            .Should().BeOfType<BoringHeroBlock>().Subject;

        mapped.ResponsiveStyle.Base.BackgroundColor.Should().Be(new CssColor("#112233"));
        mapped.ResponsiveStyle.Base.Padding.BlockStart.Should().Be(Pixels(24));
        mapped.ResponsiveStyle.Mobile!.Padding!.BlockStart.Should().Be(Pixels(8));
        mapped.ResponsiveStyle.Mobile.Direction.Should().Be(ContentDirection.RightToLeft);
        mapped.ResponsiveStyle.Should().NotBeSameAs(editorBlock.Style);
    }

    [Test]
    public async Task WrapperRendersDesktopTabletAndMobileRules()
    {
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle
            {
                Margin = Uniform(16),
                ForegroundColor = new CssColor("#123456")
            },
            Tablet = new NodeStyleOverride
            {
                Margin = UniformOverride(12)
            },
            Mobile = new NodeStyleOverride
            {
                Margin = UniformOverride(4),
                Hidden = true
            }
        };

        var html = await RenderWrapperAsync(style);

        html.Should().Contain("margin-block-start:16px");
        html.Should().Contain("color:#123456");
        html.Should().Contain("@media (max-width: 1023px)");
        html.Should().Contain("margin-block-start:12px");
        html.Should().Contain("@media (max-width: 639px)");
        html.Should().Contain("margin-block-start:4px");
        html.Should().Contain("display:none");
    }

    private static async Task<string> RenderWrapperAsync(ResponsiveNodeStyle style)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadzenComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(ResponsiveBlockStyleWrapper.Style)] = style,
                    [nameof(ResponsiveBlockStyleWrapper.ChildContent)] =
                        (RenderFragment)(builder => builder.AddContent(0, "Content"))
                });
            var output = await renderer.RenderComponentAsync<ResponsiveBlockStyleWrapper>(
                parameters);
            return output.ToHtmlString();
        });
    }

    private static CssLength Pixels(decimal value) =>
        new(value, CssLengthUnit.Pixels);

    private static LogicalSpacing Uniform(decimal value)
    {
        var length = Pixels(value);
        return new LogicalSpacing
        {
            BlockStart = length,
            BlockEnd = length,
            InlineStart = length,
            InlineEnd = length
        };
    }

    private static LogicalSpacingOverride UniformOverride(decimal value)
    {
        var length = Pixels(value);
        return new LogicalSpacingOverride
        {
            BlockStart = length,
            BlockEnd = length,
            InlineStart = length,
            InlineEnd = length
        };
    }
}
