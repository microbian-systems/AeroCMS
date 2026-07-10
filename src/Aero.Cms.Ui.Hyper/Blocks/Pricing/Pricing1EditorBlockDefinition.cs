using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

/// <summary>
/// Represents a class for Pricing1EditorBlockDefinition.
/// </summary>
public sealed class Pricing1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.pricing.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Pricing 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Three-column pricing table with highlighted plan support.";

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
public string IconName => "credit-card";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 10;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(Pricing1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(Pricing1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Pricing Plans",
            Description = "Choose the right plan for your team.",
            PricingPlans = Pricing1Block.DefaultPlans.Select(ToEditorPlan).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPricingBlock(editorBlock);
        return Pricing1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToPricingBlock(editorBlock);

    private static Pricing1Block ToPricingBlock(EditorBlock editorBlock)
    {
        return new Pricing1Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Pricing Plans"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Choose the right plan for your team."),
            Plans = editorBlock.PricingPlans.Count > 0
                ? editorBlock.PricingPlans.Select(ToPricingPlan).ToList()
                : Pricing1Block.DefaultPlans.Select(ClonePlan).ToList()
        };
    }

    private static AeroPricingPlan ToEditorPlan(Pricing1Plan plan) => new()
    {
        Name = plan.Name,
        Price = plan.Price,
        Period = plan.Period,
        Features = plan.Features.ToList(),
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        IsPopular = plan.Highlighted
    };

    private static Pricing1Plan ToPricingPlan(AeroPricingPlan plan) => new()
    {
        Name = plan.Name ?? string.Empty,
        Price = plan.Price ?? string.Empty,
        Period = string.IsNullOrWhiteSpace(plan.Period) ? "/month" : plan.Period!,
        Features = plan.Features.ToList(),
        CtaText = string.IsNullOrWhiteSpace(plan.CtaText) ? "Get Started" : plan.CtaText!,
        CtaUrl = string.IsNullOrWhiteSpace(plan.CtaUrl) ? "#" : plan.CtaUrl!,
        Highlighted = plan.IsPopular
    };

    private static Pricing1Plan ClonePlan(Pricing1Plan plan) => new()
    {
        Name = plan.Name,
        Price = plan.Price,
        Period = plan.Period,
        Features = plan.Features.ToList(),
        CtaText = plan.CtaText,
        CtaUrl = plan.CtaUrl,
        Highlighted = plan.Highlighted
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
