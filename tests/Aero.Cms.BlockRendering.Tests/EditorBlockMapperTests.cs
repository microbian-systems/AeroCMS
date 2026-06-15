using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Modules.Pages;
using FluentAssertions;
using TUnit.Core;
using NeoImageBlock = Aero.Cms.Abstractions.Blocks.Neo.ImageBlock;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class EditorBlockMapperTests
{
    [Test]
    public void MapBlocks_MapsUnsavedMarkdownAndHtmlBlocks()
    {
        List<EditorBlock> editorBlocks =
        [
            new()
            {
                Type = "markdown",
                Content = "# Unsaved markdown"
            },
            new()
            {
                Type = "raw_html",
                Content = "<p>Unsaved HTML</p>"
            }
        ];

        var blocks = EditorBlockMapper.MapBlocks(editorBlocks);

        blocks.Should().HaveCount(2);
        blocks[0].Should().BeOfType<MarkdownBlock>()
            .Which.Content.Should().Be("# Unsaved markdown");
        blocks[1].Should().BeOfType<RawHtmlBlock>()
            .Which.Content.Should().Be("<p>Unsaved HTML</p>");
    }

    [Test]
    public void MapBlock_MapsColumnsWithoutPersistedBlockIds()
    {
        var editorBlock = new EditorBlock
        {
            Type = "columns",
            ColumnCount = 2,
            Gap = 24,
            EditorColumns =
            [
                new EditorColumn
                {
                    Blocks =
                    [
                        new NestedBlock
                        {
                            Type = "text",
                            Content = "Left column"
                        }
                    ]
                },
                new EditorColumn
                {
                    Blocks =
                    [
                        new NestedBlock
                        {
                            Type = "button",
                            Text = "Read more",
                            Url = "/more",
                            Style = "secondary"
                        }
                    ]
                }
            ]
        };

        var block = EditorBlockMapper.MapBlock(editorBlock);

        var columns = block.Should().BeOfType<ColumnsBlock>().Subject;
        columns.Gap.Should().Be("24px");
        columns.Columns.Should().HaveCount(2);
        columns.Columns[0].Blocks[0].Should().BeOfType<RichTextBlock>()
            .Which.Content.Should().Be("Left column");
        columns.Columns[1].Blocks[0].Should().BeOfType<CtaBlock>()
            .Which.Text.Should().Be("Read more");
    }

    [Test]
    public void MapBlock_WithNestedImage_ProducesNeoImageBlock()
    {
        var editorBlock = new EditorBlock
        {
            Type = "columns",
            ColumnCount = 1,
            Gap = 0,
            EditorColumns =
            [
                new EditorColumn
                {
                    Blocks =
                    [
                        new NestedBlock
                        {
                            Type = "image",
                            Src = "https://example.com/photo.jpg",
                            Alt = "Example image"
                        }
                    ]
                }
            ]
        };

        var block = EditorBlockMapper.MapBlock(editorBlock);

        var columns = block.Should().BeOfType<ColumnsBlock>().Subject;
        columns.Columns.Should().HaveCount(1);
        var nestedImage = columns.Columns[0].Blocks[0].Should().BeOfType<NeoImageBlock>().Subject;
        nestedImage.Src.Should().Be("https://example.com/photo.jpg");
        nestedImage.Alt.Should().Be("Example image");
    }
}
