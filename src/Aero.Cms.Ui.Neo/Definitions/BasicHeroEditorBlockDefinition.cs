using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.Hero01;

namespace Aero.Cms.Ui.Neo.Definitions;

/// <summary>
/// Represents a class for BasicHeroEditorBlockDefinition.
/// </summary>
public sealed class BasicHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "aero.hero.basic";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Basic Hero";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "A hero section with headline, description, and CTA.";
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Components";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "layout";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 20;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(BasicHeroBlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(BasicHeroBlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = CatalogId,
        MainText = "Welcome",
        SubText = "Your message goes here.",
        CtaText = "",
        CtaUrl = "",
        BackgroundImage = string.Empty,
        FullWidth = true
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return BasicHeroBlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static BasicHeroBlock ToBlock(EditorBlock editor) => new()
    {
        Title = FirstNonEmpty(editor.MainText, "Welcome"),
        Subtitle = FirstNonEmpty(editor.SubText, "Your message goes here."),
        BackgroundImageUrl = editor.BackgroundImage,
        CtaText = editor.CtaText,
        CtaUrl = editor.CtaUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
