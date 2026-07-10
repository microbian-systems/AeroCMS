using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

/// <summary>
/// Represents a class for Pricing2EditorBlockDefinition.
/// </summary>
public sealed class Pricing2EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.pricing.2";
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Pricing 2";
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Three-column pricing table with included/not-included features.";
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
public string IconName => "dollar-sign";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 11;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Pricing2BlockEditorPreview);
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Pricing2BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            PricingPlans = Pricing2Block.DefaultPlans.Select(ToEditorPlan).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPricingBlock(editorBlock);
        return Pricing2BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToPricingBlock(editorBlock);

    private static Pricing2Block ToPricingBlock(EditorBlock editorBlock)
    {
        return new Pricing2Block
        {
            Plans = editorBlock.PricingPlans.Count > 0
                ? editorBlock.PricingPlans.Select(ToPricingPlan).ToList()
                : Pricing2Block.DefaultPlans.Select(ClonePlan).ToList()
        };
    }

    private static AeroPricingPlan ToEditorPlan(Pricing2Plan plan) => new()
    {
        Name = plan.Name,
        Description = plan.Description,
        Price = plan.Price,
        Period = plan.Period,
        Features = plan.Features.Select(f => f.Text).ToList(),
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl
    };

    private static Pricing2Plan ToPricingPlan(AeroPricingPlan plan) => new()
    {
        Name = plan.Name ?? string.Empty,
        Description = plan.Description ?? string.Empty,
        Price = plan.Price ?? string.Empty,
        Period = string.IsNullOrWhiteSpace(plan.Period) ? "/month" : plan.Period!,
        CtaText = string.IsNullOrWhiteSpace(plan.CtaText) ? "Get Started" : plan.CtaText!,
        CtaUrl = string.IsNullOrWhiteSpace(plan.CtaUrl) ? "#" : plan.CtaUrl!,
        Features = plan.Features.Select(f => new Pricing2Feature { Text = f, Included = true }).ToList()
    };

    private static Pricing2Plan ClonePlan(Pricing2Plan plan) => new()
    {
        Name = plan.Name,
        Description = plan.Description,
        Price = plan.Price,
        Period = plan.Period,
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        Features = plan.Features.Select(f => new Pricing2Feature { Text = f.Text, Included = f.Included }).ToList()
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
