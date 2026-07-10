using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Polls;

/// <summary>
/// Represents a class for Poll1EditorBlockDefinition.
/// </summary>
public sealed class Poll1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.polls.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Poll 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Single-choice poll with progress bars.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Hyper";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "bar-chart-2";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 132;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Poll1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Poll1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Where should we go for lunch?",
            Description = "Single-choice poll"
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPollBlock(editorBlock);
        return Poll1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
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
