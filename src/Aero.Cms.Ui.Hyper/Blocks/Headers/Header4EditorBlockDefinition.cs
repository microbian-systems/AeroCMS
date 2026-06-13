using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Headers;

public sealed class Header4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.headers.4";

    public string DisplayName => "Header 4";

    public string? Description => "Top navigation bar with logo, nav links, user avatar dropdown, and logout.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-top";

    public int SortOrder => 33;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Header4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Header4BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToHeaderBlock(editorBlock);
        return Header4BlockMapper.ToNode(block);
    }

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
