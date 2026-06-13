using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Buttons;

public sealed class Button8EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.buttons.8";

    public string DisplayName => "Button 8";

    public string? Description => "Rotate on hover button with solid/bordered and positive/negative rotation variants.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "square";

    public int SortOrder => 142;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Button8BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Button8BlockEditor);

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
        return Button8BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToButtonBlock(editorBlock);

    private static Button8Block ToButtonBlock(EditorBlock editorBlock)
    {
        return new Button8Block
        {
            Text = FirstNonEmpty(editorBlock.CtaText, editorBlock.Title, "Download"),
            Url = FirstNonEmpty(editorBlock.CtaUrl, "#"),
            Style = FirstNonEmpty(editorBlock.Button1Style, "solid"),
            RotateDirection = FirstNonEmpty(editorBlock.SubText, "positive")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
