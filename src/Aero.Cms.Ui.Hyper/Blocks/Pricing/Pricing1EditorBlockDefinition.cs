using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

public sealed class Pricing1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.pricing.1";

    public string DisplayName => "Pricing 1";

    public string? Description => "Three-column pricing table with highlighted plan support.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "credit-card";

    public int SortOrder => 10;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(Pricing1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(Pricing1BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToPricingBlock(editorBlock);
        return Pricing1BlockMapper.ToNode(block);
    }

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
