using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

public sealed class Header2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.headers.2";

    public string DisplayName => "Header 2";

    public string? Description => "Top navigation bar with left-aligned logo, centered nav, and login/register buttons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-top";

    public int SortOrder => 31;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Header2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Header2BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToHeaderBlock(editorBlock);
        return Header2BlockMapper.ToNode(block);
    }

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
