using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroAiContentExposureTests
{
    [Test]
    public async Task Public_ai_eligibility_requires_publication_search_and_ai_inclusion()
    {
        await Assert.That(AeroAiContentExposureRules.IsEligibleForPublicAi(
            isPublished: true,
            includeInSearch: true,
            includeInPublicAi: true)).IsTrue();

        await Assert.That(AeroAiContentExposureRules.IsEligibleForPublicAi(
            isPublished: false,
            includeInSearch: true,
            includeInPublicAi: true)).IsFalse();

        await Assert.That(AeroAiContentExposureRules.IsEligibleForPublicAi(
            isPublished: true,
            includeInSearch: false,
            includeInPublicAi: true)).IsFalse();

        await Assert.That(AeroAiContentExposureRules.IsEligibleForPublicAi(
            isPublished: true,
            includeInSearch: true,
            includeInPublicAi: false)).IsFalse();
    }

    [Test]
    public async Task Standard_audience_rules_never_expose_sensitive_or_secret_fields()
    {
        foreach (var audience in Enum.GetValues<AeroAiAudience>())
        {
            await Assert.That(AeroAiContentExposureRules.IsFieldAvailable(
                audience,
                AeroAiFieldExposure.Sensitive)).IsFalse();
            await Assert.That(AeroAiContentExposureRules.IsFieldAvailable(
                audience,
                AeroAiFieldExposure.Secret)).IsFalse();
        }
    }

    [Test]
    public async Task Internal_fields_are_manager_only_and_new_models_fail_closed()
    {
        await Assert.That(AeroAiContentExposureRules.IsFieldAvailable(
            AeroAiAudience.Public,
            AeroAiFieldExposure.Internal)).IsFalse();
        await Assert.That(AeroAiContentExposureRules.IsFieldAvailable(
            AeroAiAudience.Member,
            AeroAiFieldExposure.Internal)).IsFalse();
        await Assert.That(AeroAiContentExposureRules.IsFieldAvailable(
            AeroAiAudience.Mcp,
            AeroAiFieldExposure.Internal)).IsFalse();
        await Assert.That(AeroAiContentExposureRules.IsFieldAvailable(
            AeroAiAudience.Manager,
            AeroAiFieldExposure.Internal)).IsTrue();

        var contentType = new ContentTypeDefinition();
        var field = new ContentFieldDefinition();
        await Assert.That(contentType.IncludeInSearch).IsTrue();
        await Assert.That(contentType.IncludeInPublicAi).IsFalse();
        await Assert.That(field.AiExposure).IsEqualTo(AeroAiFieldExposure.Internal);
    }
}
