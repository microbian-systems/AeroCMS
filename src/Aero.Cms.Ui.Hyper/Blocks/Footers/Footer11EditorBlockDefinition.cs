using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer11EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.11";
    public string DisplayName => "Footer 11";
    public string? Description => "Simple footer with logo and copyright.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "panel-bottom";
    public int SortOrder => 50;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Footer11BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Footer11BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Description = "Copyright &copy; 2022. All rights reserved."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer11BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer11Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer11Block
        {
            Copyright = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.MainText, "Copyright &copy; 2022. All rights reserved.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
