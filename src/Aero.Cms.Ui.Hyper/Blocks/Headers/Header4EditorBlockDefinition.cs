using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// Represents a class for Header4EditorBlockDefinition.
/// </summary>
public sealed class Header4EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.headers.4";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Header 4";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Top navigation bar with logo, nav links, user avatar dropdown, and logout.";

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
public string IconName => "panel-top";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 33;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Header4BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Header4BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Header 4",
            CtaText = "Logout",
            CtaUrl = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToHeaderBlock(editorBlock);
        return Header4BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToHeaderBlock(editorBlock);

    private static Header4Block ToHeaderBlock(EditorBlock editorBlock)
    {
        return new Header4Block
        {
            NavLinks = Header4Block.DefaultNavLinks.Select(CloneNavLink).ToList(),
            UserMenuItems = Header4Block.DefaultUserMenuItems.Select(CloneNavLink).ToList(),
            LogoutText = FirstNonEmpty(editorBlock.CtaText, "Logout"),
            LogoutUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static HyperNavLink CloneNavLink(HyperNavLink link) => new()
    {
        Label = link.Label,
        Url = link.Url
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
