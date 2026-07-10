using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

/// <summary>
/// Represents a class for AeroHeroDefinition.
/// </summary>
public sealed class AeroHeroDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "aero_hero";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Aero Hero";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => null;
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Aero UX";
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "block";
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "layout";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 0;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(AeroHeroPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => null;

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock() => new()
    {
        Type = "aero_hero",
        MainText = "Hero Title",
        SubText = "Hero subtitle here",
        CtaText = "Get Started",
        CtaUrl = "#"
    };

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) => null!;
        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => null;
}
