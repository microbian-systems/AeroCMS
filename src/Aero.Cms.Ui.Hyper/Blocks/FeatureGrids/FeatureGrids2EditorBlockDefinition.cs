using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

public sealed class FeatureGrids2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.feature-grids.2";

    public string DisplayName => "Feature Grid 2";

    public string? Description => "Two-column feature grid with headline and stacked icon list.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "layout-grid";

    public int SortOrder => 21;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(FeatureGrids2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(FeatureGrids2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Features for growth",
            Description = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Veritatis tenetur, nemo quam voluptas sunt impedit dolorem asperiores aliquid doloribus fugit.",
            FeatureItems = FeatureGrids2Block.DefaultItems.Select(ToEditorFeatureItem).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFeatureGridsBlock(editorBlock);
        return FeatureGrids2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFeatureGridsBlock(editorBlock);

    private static FeatureGrids2Block ToFeatureGridsBlock(EditorBlock editorBlock)
    {
        return new FeatureGrids2Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Features for growth"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Veritatis tenetur, nemo quam voluptas sunt impedit dolorem asperiores aliquid doloribus fugit."),
            Items = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToFeatureGridsItem).ToList()
                : FeatureGrids2Block.DefaultItems.Select(CloneItem).ToList()
        };
    }

    private static AeroFeatureItem ToEditorFeatureItem(FeatureGrids2Item item) => new()
    {
        Icon = item.Icon,
        Title = item.Title,
        Description = item.Description,
        LinkUrl = item.LinkUrl
    };

    private static FeatureGrids2Item ToFeatureGridsItem(AeroFeatureItem item) => new()
    {
        Icon = item.Icon ?? string.Empty,
        Title = item.Title ?? string.Empty,
        Description = item.Description ?? string.Empty,
        LinkUrl = item.LinkUrl
    };

    private static FeatureGrids2Item CloneItem(FeatureGrids2Item item) => new()
    {
        Icon = item.Icon,
        Title = item.Title,
        Description = item.Description,
        LinkUrl = item.LinkUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
