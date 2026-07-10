using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// Represents a class for LogoClouds2EditorBlockDefinition.
/// </summary>
public sealed class LogoClouds2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.logo-clouds.2";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Logo Clouds 2";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Centered title + subtitle + grid of logo SVGs.";
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
public int SortOrder => 74;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(LogoClouds2BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(LogoClouds2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Trusted by many",
            Description = "Lorem, ipsum dolor sit amet consectetur adipisicing elit."
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToLogoCloudsBlock(editorBlock);
        return LogoClouds2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds2Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds2Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Trusted by many"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem, ipsum dolor sit amet consectetur adipisicing elit."),
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
