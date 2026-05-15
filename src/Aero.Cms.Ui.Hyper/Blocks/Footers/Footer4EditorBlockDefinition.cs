using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

public sealed class Footer4EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.footers.4";

    public string DisplayName => "Footer 4";

    public string? Description => "Centered CTA footer with bottom legal links and social icons.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "panel-bottom";

    public int SortOrder => 43;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Footer4BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Footer4BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Customise Your Product",
            Description = "Lorem ipsum dolor, sit amet consectetur adipisicing elit. Cum maiores ipsum eos temporibus ea nihil.",
            CtaText = "Get Started",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFooterBlock(editorBlock);
        return Footer4BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFooterBlock(editorBlock);

    private static Footer4Block ToFooterBlock(EditorBlock editorBlock)
    {
        return new Footer4Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.SectionTitle, "Customise Your Product"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor..."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Get Started"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
