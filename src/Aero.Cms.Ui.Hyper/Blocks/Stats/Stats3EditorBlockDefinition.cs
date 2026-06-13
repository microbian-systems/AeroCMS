using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Stats;

public sealed class Stats3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.stats.3";

    public string DisplayName => "Stats 3";

    public string? Description => "Stat cards with blue background.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "bar-chart";

    public int SortOrder => 65;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Stats3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Stats3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Trusted by eCommerce Businesses",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Ratione dolores laborum labore provident impedit esse recusandae facere libero harum sequi.",
            FeatureItems = Stats1Block.DefaultStats.Select(ToEditorItem).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToStatsBlock(editorBlock);
        return Stats3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToStatsBlock(editorBlock);

    private static Stats3Block ToStatsBlock(EditorBlock editorBlock)
    {
        return new Stats3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Trusted by eCommerce Businesses"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit amet consectetur adipisicing elit. Ratione dolores laborum labore provident impedit esse recusandae facere libero harum sequi."),
            Stats = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToStatItem).ToList()
                : Stats1Block.DefaultStats.Select(CloneStat).ToList()
        };
    }

    private static AeroFeatureItem ToEditorItem(StatItem stat) => new()
    {
        Title = stat.Label,
        Description = stat.Value
    };

    private static StatItem ToStatItem(AeroFeatureItem item) => new()
    {
        Label = item.Title ?? string.Empty,
        Value = item.Description ?? string.Empty
    };

    private static StatItem CloneStat(StatItem stat) => new()
    {
        Label = stat.Label,
        Value = stat.Value
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
