using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// Represents a class for Header1EditorBlockDefinition.
/// </summary>
public sealed class Header1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.headers.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Header 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Top navigation bar with logo, nav links, and login/register buttons.";

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
public int SortOrder => 30;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Header1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Header1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Header 1",
            CtaText = "Login",
            CtaUrl = "#",
            CtaText2 = "Register",
            CtaUrl2 = "#"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToHeaderBlock(editorBlock);
        return Header1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToHeaderBlock(editorBlock);

    private static Header1Block ToHeaderBlock(EditorBlock editorBlock)
    {
        return new Header1Block
        {
            NavLinks = Header1Block.DefaultNavLinks.Select(CloneNavLink).ToList(),
            LoginText = FirstNonEmpty(editorBlock.CtaText, "Login"),
            LoginUrl = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            RegisterText = FirstNonEmpty(editorBlock.CtaText2, "Register"),
            RegisterUrl = FirstNonEmpty(editorBlock.CtaUrl2, "#")
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
