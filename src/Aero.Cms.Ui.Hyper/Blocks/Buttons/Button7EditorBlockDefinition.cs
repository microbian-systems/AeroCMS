using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public sealed class Button7EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.buttons.7";

    public string DisplayName => "Button 7";

    public string? Description => "Scale on hover button with shadow, solid and bordered variants.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 141;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Button7BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Button7BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            CtaText = "Download",
            CtaUrl = "#",
            Button1Style = "solid"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToButtonBlock(editorBlock);
        return Button7BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button7Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button7Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            Style = FirstNonEmpty(editorBlock.Button1Style, "solid")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
