using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Modules.Ai.Knowledge;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroDocumentationKnowledgeSourceTests
{
    [Test]
    public void Embedded_manager_corpus_contains_the_generated_commerce_documentation()
    {
        var source = new EmbeddedAeroDocumentationKnowledgeSource();

        var matches = source.Search(
            "Commerce Stripe PayPal subscriptions webhook",
            take: 8);

        matches.ShouldNotBeEmpty();
        matches.ShouldContain(match =>
            match.SourceKind == AeroAiKnowledgeSourceKinds.AeroDocumentation
            && match.SourceUri.StartsWith(
                "/guides/commerce",
                StringComparison.Ordinal)
            && match.Content.Contains(
                "subscription",
                StringComparison.OrdinalIgnoreCase));
    }
}
