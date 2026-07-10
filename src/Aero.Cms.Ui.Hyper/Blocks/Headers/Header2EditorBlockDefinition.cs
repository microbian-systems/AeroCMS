using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

/// <summary>
/// Represents a class for Header2EditorBlockDefinition.
/// </summary>
public sealed class Header2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.headers.2";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Header 2";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Top navigation bar with left-aligned logo, centered nav, and login/register buttons.";

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
public int SortOrder => 31;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Header2BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Header2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Header 2",
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
        return Header2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToHeaderBlock(editorBlock);

    private static Header2Block ToHeaderBlock(EditorBlock editorBlock)
    {
        return new Header2Block
        {
            NavLinks = Header2Block.DefaultNavLinks.Select(CloneNavLink).ToList(),
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
