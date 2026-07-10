using System.Collections.Immutable;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Represents a class for ContentValidationService.
/// </summary>
public sealed class ContentValidationService(
    IContentTypeService contentTypeService,
    IEnumerable<IContentFieldValidator> fieldValidators,
    IEnumerable<IAsyncContentValidator> asyncValidators)
{
        /// <summary>
    /// ValidateAsync method.
    /// </summary>
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
