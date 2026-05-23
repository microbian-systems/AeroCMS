using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.2";

    public string DisplayName => "Footer 2";

    public string? Description => "Logo, social links, and link columns footer.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 41;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Footer 2"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer2Block ToFooterBlock(EditorBlock editorBlock) => new();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
