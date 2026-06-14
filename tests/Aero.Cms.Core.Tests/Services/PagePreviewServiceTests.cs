using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Railway;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Services;

public sealed class PagePreviewServiceTests
{
    [Test]
    public async Task PreviewBuildsMixedCompositionLayoutWithoutMutatingPublishedLayout()
    {
        const long pageId = 601;
        const long compositionBlockId = 701;
        var publishedLayout = new List<LayoutRegion>
        {
            new() { Name = "published", Order = 0 }
        };
        var page = new PageDocument
        {
            Id = pageId,
            SiteId = 42,
            Title = "RTL preview",
            Slug = "rtl-preview",
            Path = "/rtl-preview",
            Culture = "ar-SA",
            PublicationState = ContentPublicationState.Published,
            LayoutRegions = publishedLayout
        };
        var editor = new PageEditorState
        {
            Id = pageId,
            SiteId = 42,
            DraftVersion = 5,
            Blocks =
            [
                new EditorBlockPlacement
                {
                    ClientId = "composition",
                    BlockId = compositionBlockId,
                    Region = "main",
                    Order = 0
                }
            ]
        };
        var composition = new NeoCompositionBlock
        {
            Id = compositionBlockId,
            Nodes = [CreateCompositionRoot()]
        };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<PageDocument>(pageId, Arg.Any<CancellationToken>())
            .Returns(page);
        session.LoadAsync<PageEditorState>(pageId, Arg.Any<CancellationToken>())
            .Returns(editor);
        var blocks = Substitute.For<IBlockService>();
        blocks.GetByIdsAsync(
                Arg.Any<IEnumerable<long>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, BlockBase>
            {
                [compositionBlockId] = composition
            });
        var service = new PagePreviewService(
            session,
            new PageLayoutManifestBuilder(),
            blocks,
            NullLogger<PagePreviewService>.Instance);

        var result = await service.BuildPreviewAsync(pageId);

        var preview = result as Result<PreviewRenderModel, AeroError>.Ok;
        await Assert.That(preview).IsNotNull();
        await Assert.That(preview!.Value.IsDraft).IsTrue();
        await Assert.That(preview.Value.PageMeta.Culture).IsEqualTo("ar-SA");
        await Assert.That(preview.Value.PreviewLayout.Count).IsEqualTo(1);
        await Assert.That(
                preview.Value.PreviewLayout[0].Columns[0].Blocks[0].BlockType)
            .IsEqualTo("neo_composition");
        await Assert.That(page.LayoutRegions).IsSameReferenceAs(publishedLayout);
        await Assert.That(page.LayoutRegions[0].Name).IsEqualTo("published");
        await blocks.Received(1).GetByIdsAsync(
            Arg.Is<IEnumerable<long>>(ids =>
                ids.SequenceEqual(new[] { compositionBlockId })),
            Arg.Any<CancellationToken>());
    }

    private static NeoPageNode CreateCompositionRoot() =>
        new()
        {
            NodeId = "root",
            CatalogId = "primitive.container",
            Kind = NeoPageNodeKind.Container,
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.RightToLeft
                },
                Mobile = new NodeStyleOverride { Hidden = true }
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
                        ["text"] = JsonSerializer.SerializeToElement("مرحبا")
                    }
                }
            ]
        };
}
