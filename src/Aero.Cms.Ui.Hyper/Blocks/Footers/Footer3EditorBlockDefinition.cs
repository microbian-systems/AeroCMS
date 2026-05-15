using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.3";

    public string DisplayName => "Footer 3";

    public string? Description => "Logo, description, social links, and link columns footer.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 42;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Description = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer3Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer3Block
        {
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor...")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
