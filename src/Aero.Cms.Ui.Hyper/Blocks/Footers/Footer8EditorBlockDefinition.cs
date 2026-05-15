using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer8EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.8";

    public string DisplayName => "Footer 8";

    public string? Description => "Centered logo, description, nav links, and social icons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 47;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer8BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer8BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Description = "Centered footer with navigation links and social icons."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer8BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer8Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer8Block
        {
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque.")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
