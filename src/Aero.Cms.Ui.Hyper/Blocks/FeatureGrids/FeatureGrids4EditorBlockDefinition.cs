using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

public sealed class FeatureGrids4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.feature-grids.4";

    public string DisplayName => "Feature Grid 4";

    public string? Description => "Two-column bordered feature grid with icon support.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "layout-grid";

    public int SortOrder => 23;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(FeatureGrids4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(FeatureGrids4BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Feature Grid 4",
            Description = "Features that matter.",
            FeatureItems = FeatureGrids4Block.DefaultItems.Select(ToEditorFeatureItem).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFeatureGridsBlock(editorBlock);
        return FeatureGrids4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFeatureGridsBlock(editorBlock);

    private static FeatureGrids4Block ToFeatureGridsBlock(EditorBlock editorBlock)
    {
        return new FeatureGrids4Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.SectionTitle, "Feature Grid 4"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Features that matter."),
            Items = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToFeatureGridsItem).ToList()
                : FeatureGrids4Block.DefaultItems.Select(CloneItem).ToList()
        };
    }

    private static AeroFeatureItem ToEditorFeatureItem(FeatureGrids4Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        Icon = item.SvgPath
    };

    private static FeatureGrids4Item ToFeatureGridsItem(AeroFeatureItem item) => new()
    {
        Title = item.Title ?? string.Empty,
        Description = item.Description ?? string.Empty,
        SvgPath = item.Icon ?? string.Empty
    };

    private static FeatureGrids4Item CloneItem(FeatureGrids4Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        SvgPath = item.SvgPath
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
