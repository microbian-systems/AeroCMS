using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Serialization;
using System.Globalization;
using System.Text.Json;
using TUnit.Core;

namespace Aero.Cms.Abstractions.Tests;

public sealed class ResponsiveNodeStyleTests
{
    [Test]
    public async Task Resolve_MobileInheritsTabletThenBase()
    {
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle
            {
                Width = new CssLength(100, CssLengthUnit.Percent),
                Opacity = 1,
                Direction = ContentDirection.LeftToRight,
                Padding = new LogicalSpacing
                {
                    InlineStart = new CssLength(2, CssLengthUnit.Rem)
                }
            },
            Tablet = new NodeStyleOverride
            {
                Width = new CssLength(80, CssLengthUnit.Percent),
                Direction = ContentDirection.RightToLeft
            },
            Mobile = new NodeStyleOverride
            {
                Opacity = 0.75m,
                Padding = new LogicalSpacingOverride
                {
                    BlockStart = new CssLength(1, CssLengthUnit.Rem)
                }
            }
        };

        var resolved = style.Resolve(EditorBreakpoint.Mobile);

        await Assert.That(resolved.Width).IsEqualTo(new CssLength(80, CssLengthUnit.Percent));
        await Assert.That(resolved.Opacity).IsEqualTo(0.75m);
        await Assert.That(resolved.Direction).IsEqualTo(ContentDirection.RightToLeft);
        await Assert.That(resolved.Padding.InlineStart).IsEqualTo(new CssLength(2, CssLengthUnit.Rem));
        await Assert.That(resolved.Padding.BlockStart).IsEqualTo(new CssLength(1, CssLengthUnit.Rem));
    }

    [Test]
    public async Task Resolve_DoesNotMutateBaseStyle()
    {
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle { Opacity = 1 },
            Tablet = new NodeStyleOverride { Opacity = 0.8m }
        };

        _ = style.Resolve(EditorBreakpoint.Tablet);

        await Assert.That(style.Base.Opacity).IsEqualTo(1);
    }

    [Test]
    public async Task CssLength_ToStringUsesInvariantCssSyntax()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            var length = new CssLength(1.5m, CssLengthUnit.Rem);

            await Assert.That(length.ToString()).IsEqualTo("1.5rem");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task Validator_RejectsInvalidOpacityAndAutoValue()
    {
        var validator = new ResponsiveNodeStyleValidator();
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle
            {
                Opacity = 1.5m,
                Width = new CssLength(10, CssLengthUnit.Auto)
            }
        };

        var result = await validator.ValidateAsync(style);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task Validator_AcceptsInheritedOverrides()
    {
        var validator = new ResponsiveNodeStyleValidator();
        var style = new ResponsiveNodeStyle
        {
            Base = new NodeStyle
            {
                Width = CssLength.Auto,
                Direction = ContentDirection.Inherit
            },
            Mobile = new NodeStyleOverride
            {
                Width = new CssLength(100, CssLengthUnit.Percent)
            }
        };

        var result = await validator.ValidateAsync(style);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task BlockJsonContext_RoundTripsResponsiveNodeStyle()
    {
        var node = new NeoPageNode
        {
            NodeId = "node-1",
            CatalogId = "ui.container",
            Kind = NeoPageNodeKind.Container,
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Width = new CssLength(100, CssLengthUnit.Percent),
                    Direction = ContentDirection.RightToLeft
                },
                Mobile = new NodeStyleOverride
                {
                    Width = new CssLength(20, CssLengthUnit.Rem)
                }
            }
        };

        var json = JsonSerializer.Serialize(
            node,
            BlockJsonContext.Default.NeoPageNode);
        var roundTripped = JsonSerializer.Deserialize(
            json,
            BlockJsonContext.Default.NeoPageNode);

        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.Style.Base.Direction)
            .IsEqualTo(ContentDirection.RightToLeft);
        await Assert.That(roundTripped.Style.Mobile!.Width)
            .IsEqualTo(new CssLength(20, CssLengthUnit.Rem));
    }
}
