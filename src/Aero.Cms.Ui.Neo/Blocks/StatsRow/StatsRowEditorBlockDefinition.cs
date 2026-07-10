using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.StatsRow;

/// <summary>
/// Represents a class for StatsRowEditorBlockDefinition.
/// </summary>
public sealed class StatsRowEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => StatsRowBlock.BlockTypeId;

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Status / Social Row";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A NeoUI stats row displaying value/label pairs with divider lines.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Neo";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "bar-chart-3";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 50;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => false;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(StatsRowBlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(StatsRowBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            TrustMarkers  = ["10,000+:Happy Users", "$2M+:ARR Generated", "99.9%:Uptime SLA", "4.9/5:Average Rating"],
            BackgroundImage = string.Empty
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return StatsRowBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static StatsRowBlock ToBlock(EditorBlock editor) => new()
    {
        Stats = editor.TrustMarkers.Count > 0
            ? editor.TrustMarkers
                .Select(m =>
                {
                    var parts = m.Split(':', 2);
                    return new StatItem(parts[0], parts.Length > 1 ? parts[1] : string.Empty);
                })
                .ToList()
            : [],
    };
}
