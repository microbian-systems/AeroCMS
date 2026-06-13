using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public sealed class Button9EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.buttons.9";

    public string DisplayName => "Button 9";

    public string? Description => "Bold uppercase offset shadow button with hover translate effect.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 143;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Button9BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Button9BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            CtaText = "Download",
            CtaUrl = "#",
            Button1Style = "hover-out"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToButtonBlock(editorBlock);
        return Button9BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button9Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button9Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            OffsetStyle = FirstNonEmpty(editorBlock.Button1Style, "hover-out")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
