using System.Collections.Immutable;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Orchestrates content-type resolution, synchronous field validation, and
/// publication-only asynchronous validation.
/// </summary>
/// <remarks>
/// Validation stages run in sequence. Content-type lookup failure stops validation. Synchronous
/// failures are aggregated before returning and prevent asynchronous validators from running.
/// During publication, asynchronous validators run sequentially and all returned failures are
/// aggregated.
/// </remarks>
public sealed class ContentValidationService(
    IContentTypeService contentTypeService,
    IEnumerable<IContentFieldValidator> fieldValidators,
    IEnumerable<IAsyncContentValidator> asyncValidators)
{
    /// <summary>
    /// Validates a content item under draft or publication rules.
    /// </summary>
    /// <param name="item">The content item to validate.</param>
    /// <param name="mode">The rule set to apply.</param>
    /// <param name="ct">A token that can cancel type lookup or validation.</param>
    /// <returns>
    /// The same item in a successful result when all applicable rules pass; otherwise a
    /// not-found or validation error containing the aggregated failure messages.
    /// </returns>
    /// <remarks>
    /// Asynchronous validators run only in <see cref="ContentValidationMode.Publish"/> mode.
    /// This method does not mutate or persist <paramref name="item"/>.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    public async Task<Result<ContentItem, AeroError>> ValidateAsync(
        ContentItem item, ContentValidationMode mode, CancellationToken ct = default)
    {
        var typeResult = await contentTypeService.GetByAliasAsync(item.SiteId, item.ContentTypeAlias, ct);
        if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure)
            return AeroError.NotFoundError($"Content type '{item.ContentTypeAlias}' was not found.");

        var type = ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value;

        var syncValidator = new DynamicContentValidator(type, mode, fieldValidators);
        var syncResult = await syncValidator.ValidateAsync(item, ct);
        if (!syncResult.IsValid)
            return new AeroError.Validation(syncResult.Errors.Select(e => e.ErrorMessage).ToImmutableList());

        if (mode == ContentValidationMode.Publish)
        {
            var allFailures = new List<string>();
            foreach (var asyncValidator in asyncValidators)
            {
                var failures = await asyncValidator.ValidateAsync(item, type, ct);
                allFailures.AddRange(failures.Select(f => f.ErrorMessage));
            }

            if (allFailures.Count > 0)
                return new AeroError.Validation(allFailures.ToImmutableList());
        }

        return Prelude.Ok<ContentItem, AeroError>(item);
    }
}
