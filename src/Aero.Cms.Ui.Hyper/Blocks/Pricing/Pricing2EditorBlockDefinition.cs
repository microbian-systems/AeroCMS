using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

public sealed class Pricing2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.pricing.2";
    public string DisplayName => "Pricing 2";
    public string? Description => "Three-column pricing table with included/not-included features.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "dollar-sign";
    public int SortOrder => 11;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Pricing2BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Pricing2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            PricingPlans = Pricing2Block.DefaultPlans.Select(ToEditorPlan).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPricingBlock(editorBlock);
        return Pricing2BlockMapper.ToNode(block);
    }

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
