using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

public sealed class Header1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.headers.1";

    public string DisplayName => "Header 1";

    public string? Description => "Top navigation bar with logo, nav links, and login/register buttons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-top";

    public int SortOrder => 30;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Header1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Header1BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToHeaderBlock(editorBlock);
        return Header1BlockMapper.ToNode(block);
    }

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
