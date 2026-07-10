using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// Represents a class for LogoClouds4EditorBlockDefinition.
/// </summary>
public sealed class LogoClouds4EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.logo-clouds.4";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Logo Clouds 4";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Rounded grid with background cells, no text.";
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
public string IconName => "layers";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 76;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(LogoClouds4BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(LogoClouds4BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToLogoCloudsBlock(editorBlock);
        return LogoClouds4BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds4Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds4Block
        {
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }
}
