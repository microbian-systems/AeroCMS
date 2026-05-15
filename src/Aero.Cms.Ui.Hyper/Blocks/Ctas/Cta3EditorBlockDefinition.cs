using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

public sealed class Cta3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.ctas.3";

    public string DisplayName => "CTA 3";

    public string? Description => "Side-by-side CTA with curved image and emerald button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "megaphone";

    public int SortOrder => 68;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Cta3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Cta3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor sit amet consectetur adipisicing elit",
            Description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Et, egestas tempus tellus etiam sed.",
            CtaText = "Get Started Today",
            CtaUrl = "#",
            Src = "https://images.unsplash.com/photo-1484959014842-cd1d967a39cf?auto=format&fit=crop&q=80&w=1160"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCtaBlock(editorBlock);
        return Cta3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCtaBlock(editorBlock);

    private static Cta3Block ToCtaBlock(EditorBlock editorBlock)
    {
        return new Cta3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
            CtaText = FirstNonEmpty(editorBlock.CtaText, "Get Started Today"),
            CtaUrl = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            ImageUrl = FirstNonEmpty(editorBlock.Src, editorBlock.BackgroundImage, "https://images.unsplash.com/photo-1484959014842-cd1d967a39cf?auto=format&fit=crop&q=80&w=1160")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
