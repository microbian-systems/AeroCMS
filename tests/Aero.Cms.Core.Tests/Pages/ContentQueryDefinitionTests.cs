using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Shouldly;

namespace Aero.Cms.Core.Tests.Pages;

public sealed class ContentQueryDefinitionTests
{
    [Test]
    public void Composition_snapshot_and_reconciliation_preserve_normalized_queries()
    {
        var composition = new PageCompositionDocument
        {
            ContentQueries =
            [
                new ContentQueryDefinition
                {
                    Name = " Topics ",
                    ContentTypeId = 501,
                    ContentTypeAlias = " topics ",
                    Traversal = ContentTraversal.Roots,
                    Projection = [" title ", "TITLE", "summary"]
                }
            ]
        };

        var snapshot = composition.CreateSnapshot();
        var reconciled = PageCompositionReconciler.RemoveOrphans(
            new HtmlPageContent(),
            composition);

        snapshot.ContentQueries.Single().Name.ShouldBe("topics");
        snapshot.ContentQueries.Single().Projection.ShouldBe(["title", "summary"]);
        reconciled.ContentQueries.Single().Name.ShouldBe("topics");
        reconciled.ContentQueries.Single().ContentTypeId.ShouldBe(501);
    }

    [Test]
    public async Task Structural_validation_rejects_duplicate_names_and_invalid_root_shape()
    {
        var composition = new PageCompositionDocument
        {
            ContentQueries =
            [
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "topics",
                    Traversal = ContentTraversal.Roots,
                    RootId = 7
                },
                new ContentQueryDefinition
                {
                    Name = "topics",
                    ContentTypeId = 501,
                    ContentTypeAlias = "topics",
                    Traversal = ContentTraversal.Descendants
                }
            ]
        };

        var result = await new PageCompositionValidator(new HtmlPageContent())
            .ValidateAsync(composition);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error =>
            error.ErrorMessage.Contains("cannot be declared more than once", StringComparison.Ordinal));
        result.Errors.ShouldContain(error =>
            error.ErrorMessage.Contains("cannot specify a root item", StringComparison.Ordinal));
        result.Errors.ShouldContain(error =>
            error.ErrorMessage.Contains("requires a stable root item", StringComparison.Ordinal));
    }

    [Test]
    public async Task Null_declaration_is_preserved_for_validation_and_rejected_without_throwing()
    {
        var composition = new PageCompositionDocument
        {
            ContentQueries = [null!]
        };

        var snapshot = composition.CreateSnapshot();
        var reconciled = PageCompositionReconciler.RemoveOrphans(
            new HtmlPageContent(),
            composition);
        var result = await new PageCompositionValidator(new HtmlPageContent())
            .ValidateAsync(composition);

        snapshot.ContentQueries.Single().ShouldBeNull();
        reconciled.ContentQueries.Single().ShouldBeNull();
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error =>
            error.ErrorMessage.Contains("cannot be null", StringComparison.Ordinal));
    }
}
