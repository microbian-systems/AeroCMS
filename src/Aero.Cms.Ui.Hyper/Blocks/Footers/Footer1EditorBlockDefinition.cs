using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.1";

    public string DisplayName => "Footer 1";

    public string? Description => "Newsletter signup footer with link columns and social icons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 40;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Get the latest news!",
            Description = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Esse non cupiditate quae nam molestias."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer1Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer1Block
        {
            NewsletterTitle = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.SectionTitle, "Get the latest news!"),
            NewsletterDescription = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor..."),
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
