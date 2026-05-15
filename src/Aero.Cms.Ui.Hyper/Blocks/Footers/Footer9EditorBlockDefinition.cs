using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer9EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.9";
    public string DisplayName => "Footer 9";
    public string? Description => "Simple footer with logo, description, nav links, and back-to-top button.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "panel-bottom";
    public int SortOrder => 48;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Footer9BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Footer9BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Description = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer9BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer9Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer9Block
        {
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.MainText, "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."),
            NavLinks = Footer9Block.DefaultNavLinks.Select(CloneLink).ToList(),
            Copyright = "Copyright &copy; 2022. All rights reserved."
        };
    }

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
