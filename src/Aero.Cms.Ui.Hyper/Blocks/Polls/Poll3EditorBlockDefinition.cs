using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

public sealed class Poll3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.polls.3";

    public string DisplayName => "Poll 3";

    public string? Description => "Star rating poll with 1-5 stars.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "bar-chart-2";

    public int SortOrder => 134;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Poll3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Poll3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Leave a rating",
            Description = "Star rating poll"
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPollBlock(editorBlock);
        return Poll3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToPollBlock(editorBlock);

    private static Poll3Block ToPollBlock(EditorBlock editorBlock)
    {
        return new Poll3Block
        {
            Question = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, "Leave a rating"),
            PollName = "Rating1",
            MaxRating = 5
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
