using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.StatsRow;

public sealed class StatsRowEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => StatsRowBlock.BlockTypeId;

    public string DisplayName => "Status / Social Row";

    public string? Description => "A NeoUI stats row displaying value/label pairs with divider lines.";

    public string Category => "Neo";

    public string Kind => "Block";

    public string IconName => "bar-chart-3";

    public int SortOrder => 50;

    public bool PublicStaticSsrSafe => false;

    public Type? PreviewComponentType => typeof(StatsRowBlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(StatsRowBlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            TrustMarkers  = ["10,000+:Happy Users", "$2M+:ARR Generated", "99.9%:Uptime SLA", "4.9/5:Average Rating"],
            BackgroundImage = string.Empty
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return StatsRowBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static StatsRowBlock ToBlock(EditorBlock editor) => new()
    {
        Stats = editor.TrustMarkers.Count > 0
            ? editor.TrustMarkers
                .Select(m =>
                {
                    var parts = m.Split(':', 2);
                    return new StatItem(parts[0], parts.Length > 1 ? parts[1] : string.Empty);
                })
                .ToList()
            : [],
    };
}
