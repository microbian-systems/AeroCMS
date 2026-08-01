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
        var snapshot = source.GetSnapshot();

        var matches = source.Search(
            "Commerce Stripe PayPal subscriptions webhook",
            take: 8);

        snapshot.CorpusHash.ShouldNotBeNullOrWhiteSpace();
        snapshot.Chunks.ShouldContain(chunk =>
            chunk.CanonicalPath.StartsWith(
                "/guides/commerce",
                StringComparison.Ordinal)
            && chunk.Content.Contains(
                "subscription",
                StringComparison.OrdinalIgnoreCase));
        snapshot.Chunks
            .Select(chunk => chunk.Id)
            .Distinct()
            .Count()
            .ShouldBe(snapshot.Chunks.Count);
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

    [Test]
    public void Corpus_validation_rejects_security_sensitive_entries()
    {
        var corpus = ValidCorpus() with
        {
            Entries =
            [
                ValidCorpus().Entries[0] with
                {
                    Audience = "security-sensitive"
                }
            ]
        };

        var exception = Should.Throw<InvalidOperationException>(
            () => AeroDocumentationCorpusValidator.Validate(corpus));

        exception.Message.ShouldContain("unsupported audience");
    }

    [Test]
    public void Corpus_validation_rejects_duplicate_canonical_paths()
    {
        var entry = ValidCorpus().Entries[0];
        var corpus = ValidCorpus() with { Entries = [entry, entry] };

        var exception = Should.Throw<InvalidOperationException>(
            () => AeroDocumentationCorpusValidator.Validate(corpus));

        exception.Message.ShouldContain("duplicate canonical path");
    }

    private static AeroDocumentationCorpus ValidCorpus()
        => new(
            SchemaVersion: 1,
            Product: "AeroCMS",
            LastVerifiedCommit: "test-commit",
            TrustClass: "manager-internal",
            Entries:
            [
                new AeroDocumentationCorpusEntry(
                    Title: "Commerce",
                    CanonicalPath: "/guides/commerce",
                    FeatureArea: "Commerce",
                    Maturity: "stable",
                    Audience: "public",
                    SourceFiles: ["docs/src/content/docs/guides/commerce.md"],
                    Content: "Commerce documentation.")
            ]);
}
