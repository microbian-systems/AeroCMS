using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.LogoClouds;

/// <summary>
/// Represents a class for LogoClouds3EditorBlockDefinition.
/// </summary>
public sealed class LogoClouds3EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.logo-clouds.3";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Logo Clouds 3";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Left-aligned title + rounded grid with background cells.";
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
public int SortOrder => 75;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(LogoClouds3BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(LogoClouds3BlockEditor);

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
        return LogoClouds3BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToLogoCloudsBlock(editorBlock);

    private static LogoClouds3Block ToLogoCloudsBlock(EditorBlock editorBlock)
    {
        return new LogoClouds3Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, "Trusted by many"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, "Lorem, ipsum dolor sit amet consectetur adipisicing elit."),
            LogoItems = LogoCloudsDefaults.CloneDefaults()
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
