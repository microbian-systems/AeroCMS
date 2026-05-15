using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

public sealed class Poll1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.polls.1";

    public string DisplayName => "Poll 1";

    public string? Description => "Single-choice poll with progress bars.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "bar-chart-2";

    public int SortOrder => 132;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Poll1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Poll1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Where should we go for lunch?",
            Description = "Single-choice poll"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPollBlock(editorBlock);
        return Poll1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToPollBlock(editorBlock);

    private static Poll1Block ToPollBlock(EditorBlock editorBlock)
    {
        return new Poll1Block
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
