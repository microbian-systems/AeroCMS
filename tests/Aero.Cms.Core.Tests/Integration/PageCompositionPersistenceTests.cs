using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using FluentAssertions;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PageCompositionPersistenceTests
{
    [Test]
    public async Task DetachedPageSnapshotPreservesMixedResponsiveComposition()
    {
        var harness = new InMemoryCmsDocumentSessionHarness();
        var page = new PageDocument
        {
            Id = 501,
            SiteId = 42,
            TranslationGroupId = 501,
            Culture = "ar-SA",
            Title = "Mixed editor page",
            Slug = "mixed-editor-page",
            Path = "/mixed-editor-page",
            PublicationState = ContentPublicationState.Published,
            PublishedVersion = 3,
            Blocks =
            [
                new EditorBlock
                {
                    EditorId = "hero",
                    Type = "hero",
                    MainText = "Existing canned hero"
                },
                new EditorBlock
                {
                    EditorId = "composition",
                    Type = "neo.composition",
                    CompositionNodes =
                    [
                        new NeoPageNode
                        {
                            NodeId = "container",
                            CatalogId = "primitive.container",
                            Kind = NeoPageNodeKind.Container,
                            Style = new ResponsiveNodeStyle
                            {
                                Base = new NodeStyle
                                {
                                    Direction = ContentDirection.RightToLeft,
                                    Padding = new LogicalSpacing
                                    {
                                        InlineStart = new CssLength(
                                            24,
                                            CssLengthUnit.Pixels)
                                    }
                                },
                                Mobile = new NodeStyleOverride
                                {
                                    Hidden = true
                                }
                            },
                            Children =
                            [
                                new NeoPageNode
                                {
                                    NodeId = "text",
                                    CatalogId = "primitive.text",
                                    Kind = NeoPageNodeKind.Primitive,
                                    Properties = new Dictionary<string, JsonElement>
                                    {
                                        ["text"] =
                                            JsonSerializer.SerializeToElement(
                                                "مرحبا")
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        harness.StorePage(page);
        var restored = await harness.LoadPageAsync(page.Id);

        restored.Should().NotBeNull();
        restored!.Should().NotBeSameAs(page);
        restored.Culture.Should().Be("ar-SA");
        restored.PublicationState.Should().Be(ContentPublicationState.Published);
        restored.PublishedVersion.Should().Be(3);
        restored.Blocks.Should().HaveCount(2);
        restored.Blocks[0].MainText.Should().Be("Existing canned hero");

        var root = restored.Blocks[1].CompositionNodes.Should()
            .ContainSingle()
            .Subject;
        root.Style.Base.Direction.Should().Be(ContentDirection.RightToLeft);
        root.Style.Base.Padding.InlineStart.Should().Be(
            new CssLength(24, CssLengthUnit.Pixels));
        root.Style.Mobile!.Hidden.Should().BeTrue();
        root.Children.Should().ContainSingle()
            .Which.Properties["text"].GetString().Should().Be("مرحبا");

        restored.Blocks[1].CompositionNodes[0].Children[0]
            .Properties["text"] = JsonSerializer.SerializeToElement("changed");
        page.Blocks[1].CompositionNodes[0].Children[0]
            .Properties["text"].GetString().Should().Be("مرحبا");
    }
}
