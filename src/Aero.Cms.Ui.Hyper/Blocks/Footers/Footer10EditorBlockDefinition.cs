using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer10EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.10";
    public string DisplayName => "Footer 10";
    public string? Description => "Footer with logo, description, social icons, four link columns, and legal links.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "panel-bottom";
    public int SortOrder => 49;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Footer10BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Footer10BlockEditor);

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
        return Footer10BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer10Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer10Block
        {
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.MainText, "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."),
            SocialLinks = FooterDefaults.DefaultSocialLinks.Select(FooterDefaults.CloneSocialLink).ToList(),
            LinkColumns = Footer10Block.DefaultLinkColumns.Select(CloneColumn).ToList(),
            Copyright = "&copy; 2022 Company Name",
            LegalLinks = Footer10Block.DefaultLegalLinks.Select(CloneLink).ToList()
        };
    }

    private static FooterLinkColumn CloneColumn(FooterLinkColumn col) => new()
    {
        Title = col.Title,
        Links = col.Links.Select(l => new FooterLink { Text = l.Text, Url = l.Url }).ToList()
    };

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
