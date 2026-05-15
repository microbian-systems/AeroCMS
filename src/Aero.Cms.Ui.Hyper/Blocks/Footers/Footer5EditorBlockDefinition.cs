using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer5EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.5";

    public string DisplayName => "Footer 5";

    public string? Description => "Image with contact info, social links, and link columns.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 44;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer5BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer5BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Call us",
            Description = "Footer with contact info, links, and image."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer5BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer5Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer5Block
        {
            CallUsText = FirstNonEmpty(editorBlock.MainText, "Call us"),
            CopyrightText = FirstNonEmpty(editorBlock.Description, "&copy; 2022. Company Name. All rights reserved.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
