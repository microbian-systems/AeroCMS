using Aero.Cms.Abstractions.Content.Importing;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content.Importing;

public sealed class ContentImportContractsTests
{
    [Test]
    public void Request_identity_is_deterministic_for_the_full_pinned_request()
    {
        var first = Request();
        var second = Request();

        first.Identity.ShouldBe(second.Identity);
        (first with { SelectionFingerprint = "different-selection" }).Identity.ShouldNotBe(first.Identity);
        (first with { SiteId = 99 }).Identity.ShouldNotBe(first.Identity);
    }

    [Test]
    public void Request_requires_a_site_but_never_accepts_a_tenant()
    {
        (Request() with { SiteId = 0 }).IsValid.ShouldBeFalse();
        Request().IsValid.ShouldBeTrue();
    }

    [Test]
    public void Request_identity_uses_canonical_json_and_cannot_be_ambiguous_at_field_boundaries()
    {
        var first = Request() with { OptionsJson = "{\"b\":2,\"a\":1}" };
        var equivalent = first with { OptionsJson = " { \"a\" : 1, \"b\" : 2 } " };
        var boundaryOne = Request() with { ImporterKey = "alpha\u001fbeta", ImporterVersion = "gamma" };
        var boundaryTwo = Request() with { ImporterKey = "alpha", ImporterVersion = "beta\u001fgamma" };

        first.Identity.ShouldBe(equivalent.Identity);
        boundaryOne.Identity.ShouldNotBe(boundaryTwo.Identity);
    }

    [Test]
    public void Request_rejects_invalid_or_unbounded_options_and_actor()
    {
        (Request() with { OptionsJson = "not-json" }).IsValid.ShouldBeFalse();
        (Request() with { OptionsJson = "{\"same\":1,\"same\":2}" }).IsValid.ShouldBeFalse();
        (Request() with { OptionsJson = new string(' ', ContentImportRequest.MaximumOptionsJsonLength + 1) }).IsValid.ShouldBeFalse();
        (Request() with { Actor = new string('a', ContentImportRequest.MaximumActorLength + 1) }).IsValid.ShouldBeFalse();
    }

    [Test]
    public void Provider_failures_explicitly_distinguish_retryable_and_terminal()
    {
        ContentImportProviderResult.Failure("retry").FailureDisposition.ShouldBe(ContentImportFailureDisposition.Retryable);
        ContentImportProviderResult.Failure("stop", disposition: ContentImportFailureDisposition.Terminal).FailureDisposition.ShouldBe(ContentImportFailureDisposition.Terminal);
    }

    private static ContentImportRequest Request() => new(
        SiteId: 7,
        ImporterKey: "test-importer",
        ImporterVersion: "1",
        SourceFingerprint: "source",
        SelectionFingerprint: "selection",
        OptionsJson: "{}",
        Actor: "system:test",
        Activate: false);
}
