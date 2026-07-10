using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;

/// <summary>
/// Represents a class for FeatureGrids3EditorBlockDefinition.
/// </summary>
public sealed class FeatureGrids3EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.feature-grids.3";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Feature Grid 3";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Four-column centered feature grid with icon support.";

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
public string IconName => "layout-grid";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 22;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(FeatureGrids3BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(FeatureGrids3BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Feature Grid 3",
            Description = "Features that matter.",
            FeatureItems = FeatureGrids3Block.DefaultItems.Select(ToEditorFeatureItem).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToFeatureGridsBlock(editorBlock);
        return FeatureGrids3BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToFeatureGridsBlock(editorBlock);

    private static FeatureGrids3Block ToFeatureGridsBlock(EditorBlock editorBlock)
    {
        return new FeatureGrids3Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.SectionTitle, "Feature Grid 3"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Features that matter."),
            Items = editorBlock.FeatureItems.Count > 0
                ? editorBlock.FeatureItems.Select(ToFeatureGridsItem).ToList()
                : FeatureGrids3Block.DefaultItems.Select(CloneItem).ToList()
        };
    }

    private static AeroFeatureItem ToEditorFeatureItem(FeatureGrids3Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        Icon = item.SvgPath
    };

    private static FeatureGrids3Item ToFeatureGridsItem(AeroFeatureItem item) => new()
    {
        Title = item.Title ?? string.Empty,
        Description = item.Description ?? string.Empty,
        SvgPath = item.Icon ?? string.Empty
    };

    private static FeatureGrids3Item CloneItem(FeatureGrids3Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        SvgPath = item.SvgPath
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
