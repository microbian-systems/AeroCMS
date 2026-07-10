using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer10EditorBlockDefinition.
/// </summary>
public sealed class Footer10EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.footers.10";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Footer 10";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Footer with logo, description, social icons, four link columns, and legal links.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Hyper";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "panel-bottom";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 49;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Footer10BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Footer10BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Description = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Incidunt consequuntur amet culpa cum itaque neque."
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer10BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
