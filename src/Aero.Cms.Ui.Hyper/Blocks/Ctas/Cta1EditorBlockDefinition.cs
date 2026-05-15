using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

public sealed class Cta1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.ctas.1";

    public string DisplayName => "CTA 1";

    public string? Description => "Side-by-side CTA with image on right and emerald button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "megaphone";

    public int SortOrder => 66;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Cta1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Cta1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor sit amet consectetur adipisicing elit",
            Description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Et, egestas tempus tellus etiam sed.",
            CtaText = "Get Started Today",
            CtaUrl = "#",
            Src = "https://images.unsplash.com/photo-1464582883107-8adf2dca8a9f?auto=format&fit=crop&q=80&w=1160"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCtaBlock(editorBlock);
        return Cta1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCtaBlock(editorBlock);

    private static Cta1Block ToCtaBlock(EditorBlock editorBlock)
    {
        return new Cta1Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Get Started Today"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, editorBlock.BackgroundImage, "https://images.unsplash.com/photo-1464582883107-8adf2dca8a9f?auto=format&fit=crop&q=80&w=1160")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
