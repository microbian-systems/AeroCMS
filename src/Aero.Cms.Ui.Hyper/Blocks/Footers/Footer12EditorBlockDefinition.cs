using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer12EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.12";

    public string DisplayName => "Footer 12";

    public string? Description => "CTA banner footer with link columns and social icons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 51;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer12BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer12BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Make Your Next Career Move!",
            Description = "Let's Get Started"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer12BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer12Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer12Block
        {
            CtaTitle = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.SectionTitle, "Make Your Next Career Move!"),
            CtaText = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Let's Get Started"),
            Description = editorBlock.Content ?? "CTA banner footer with link columns and social icons.",
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
