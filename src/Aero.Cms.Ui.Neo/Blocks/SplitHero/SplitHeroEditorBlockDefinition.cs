using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero;

public sealed class SplitHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => SplitHeroBlock.BlockTypeId;

    public string DisplayName => "Hero Split Layout";

    public string? Description => "A NeoUI split hero section with eyebrow, title, description, dual CTAs, and decorative card panel.";

    public string Category => "Neo";

    public string Kind => "Block";

    public string IconName => "layout-dashboard";

    public int SortOrder => 20;

    public bool PublicStaticSsrSafe => false;

    public Type? PreviewComponentType => typeof(SplitHeroBlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(SplitHeroBlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            Eyebrow       = "New — v2.0 is here",
            MainText      = "Build better products, ship faster",
            SubText       = "The all-in-one platform that helps your team design, develop, and deliver exceptional digital experiences without the complexity.",
            CtaText       = "Get started free",
            CtaUrl        = "#",
            CtaText2      = "Watch demo",
            CtaUrl2       = "#",
            BackgroundImage = string.Empty
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return SplitHeroBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static SplitHeroBlock ToBlock(EditorBlock editor) => new()
    {
        Eyebrow       = FirstNonEmpty(editor.Eyebrow,  "New — v2.0 is here"),
        Title         = FirstNonEmpty(editor.MainText, "Build better products, ship faster"),
        Description   = FirstNonEmpty(editor.SubText,  string.Empty),
        PrimaryText   = FirstNonEmpty(editor.CtaText,  "Get started free"),
        PrimaryUrl    = FirstNonEmpty(editor.CtaUrl,   "#"),
        SecondaryText = FirstNonEmpty(editor.CtaText2, "Watch demo"),
        SecondaryUrl  = FirstNonEmpty(editor.CtaUrl2,  "#"),
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
