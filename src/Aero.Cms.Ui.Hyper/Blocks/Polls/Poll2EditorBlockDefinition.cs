using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

public sealed class Poll2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.polls.2";

    public string DisplayName => "Poll 2";

    public string? Description => "Multi-choice poll with progress bars and checkboxes.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "bar-chart-2";

    public int SortOrder => 133;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Poll2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Poll2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Where should we go for lunch?",
            Description = "Multi-choice poll"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPollBlock(editorBlock);
        return Poll2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToPollBlock(editorBlock);

    private static Poll2Block ToPollBlock(EditorBlock editorBlock)
    {
        return new Poll2Block
        {
            Question = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, "Where should we go for lunch?"),
            Description = FirstNonEmpty(editorBlock.Description, "Lorem ipsum dolor sit, amet consectetur adipisicing elit."),
            EndDate = "October 31, 2025",
            EndDateIso = "2025-10-31"
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
