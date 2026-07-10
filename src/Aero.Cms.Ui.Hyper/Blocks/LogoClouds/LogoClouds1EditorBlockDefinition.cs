using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// Represents a class for LogoClouds1EditorBlockDefinition.
/// </summary>
public sealed class LogoClouds1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.logo-clouds.1";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Logo Clouds 1";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Simple grid of grayscale logo SVGs.";
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
public int SortOrder => 73;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(LogoClouds1BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(LogoClouds1BlockEditor);

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
        return LogoClouds1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds1Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds1Block
        {
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }
}
