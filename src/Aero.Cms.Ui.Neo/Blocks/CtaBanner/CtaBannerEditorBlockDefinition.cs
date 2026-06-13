using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CtaBanner;

public sealed class CtaBannerEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => CtaBannerBlock.BlockTypeId;

    public string DisplayName => "CTA Banner";

    public string? Description => "A NeoUI call-to-action banner with title, description, and dual CTAs.";

    public string Category => "Neo";

    public string Kind => "Block";

    public string IconName => "megaphone";

    public int SortOrder => 30;

    public bool PublicStaticSsrSafe => false;

    public Type? PreviewComponentType => typeof(CtaBannerBlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(CtaBannerBlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            MainText      = "Start building for free today",
            SubText       = "Join thousands of teams already using Acme to ship faster and smarter. No credit card required.",
            CtaText       = "Get started free",
            CtaUrl        = "#",
            CtaText2      = "Schedule a demo",
            CtaUrl2       = "#",
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return CtaBannerBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static CtaBannerBlock ToBlock(EditorBlock editor) => new()
    {
        Title         = FirstNonEmpty(editor.MainText,      "Start building for free today"),
        Description   = FirstNonEmpty(editor.SubText,       string.Empty),
        PrimaryText   = FirstNonEmpty(editor.CtaText,       "Get started free"),
        PrimaryUrl    = FirstNonEmpty(editor.CtaUrl,        "#"),
        SecondaryText = FirstNonEmpty(editor.CtaText2,      "Schedule a demo"),
        SecondaryUrl  = FirstNonEmpty(editor.CtaUrl2,       "#"),
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
