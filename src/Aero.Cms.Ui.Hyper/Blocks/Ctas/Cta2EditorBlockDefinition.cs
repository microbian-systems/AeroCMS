using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Ctas;

public sealed class Cta2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.ctas.2";

    public string DisplayName => "CTA 2";

    public string? Description => "Centered CTA with email signup form and rose button.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "megaphone";

    public int SortOrder => 67;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Cta2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Cta2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem, ipsum dolor sit amet consectetur adipisicing elit",
            Description = "Lorem ipsum dolor sit amet, consectetur adipisicing elit.",
            CtaText = "Sign Up",
            CtaText2 = "Email address",
            CtaUrl = "#"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToCtaBlock(editorBlock);
        return Cta2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToCtaBlock(editorBlock);

    private static Cta2Block ToCtaBlock(EditorBlock editorBlock)
    {
        return new Cta2Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Lorem, ipsum dolor sit amet consectetur adipisicing elit"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem ipsum dolor sit amet, consectetur adipisicing elit."),
            ButtonText = FirstNonEmpty(editorBlock.CtaText, "Sign Up"),
            Placeholder = FirstNonEmpty(editorBlock.CtaText2, "Email address"),
            FormAction = FirstNonEmpty(editorBlock.CtaUrl, "#")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
