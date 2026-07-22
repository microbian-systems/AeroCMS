using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Validators;

/// <summary>
/// Composes Pages-owned HTML structure checks with Content-owned reference checks.
/// </summary>
internal static class PageCompositionValidationPipeline
{
    /// <summary>Validates one composition snapshot at an authoring or publishing boundary.</summary>
    public static async Task<Result<bool, AeroError>> ValidateAsync(
        long siteId,
        string culture,
        HtmlPageContent content,
        PageCompositionDocument composition,
        ContentReferenceValidationMode mode,
        IContentCompositionReferenceValidator? referenceValidator,
        IPageRegisteredFragmentRegistry? registeredFragmentRegistry,
        CancellationToken ct)
    {
        var structuralValidation = await new PageCompositionValidator(content)
            .ValidateAsync(composition, ct);
        if (!structuralValidation.IsValid)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.ValidationError(
                    structuralValidation.Errors.Select(error => error.ErrorMessage)));
        }

        var registeredFragments = composition.RegisteredFragments ?? [];
        if (registeredFragments.Count > 0)
        {
            if (registeredFragmentRegistry is null)
            {
                return Prelude.Fail<bool, AeroError>(
                    AeroError.ConfigurationError(
                        "The registered page-fragment registry is required to validate this composition."));
            }

            var errors = new List<string>();
            foreach (var fragment in registeredFragments)
            {
                if (registeredFragmentRegistry.Validate(fragment)
                    is Result<PageRegisteredFragment>.Failure failure)
                {
                    errors.Add(failure.Error.ToString());
                }
            }

            if (errors.Count > 0)
            {
                return Prelude.Fail<bool, AeroError>(AeroError.ValidationError(errors));
            }
        }

        var hasReferences = (composition.ContentLists?.Count ?? 0) > 0
            || (composition.ContentItems?.Count ?? 0) > 0
            || (composition.FieldBindings?.Count ?? 0) > 0;
        if (!hasReferences)
        {
            return Prelude.Ok<bool, AeroError>(true);
        }

        if (referenceValidator is null)
        {
            return Prelude.Fail<bool, AeroError>(
                AeroError.ConfigurationError(
                    "The Content module is required to validate structured page content."));
        }

        return await referenceValidator.ValidateAsync(
            siteId,
            culture,
            composition,
            mode,
            ct);
    }
}
