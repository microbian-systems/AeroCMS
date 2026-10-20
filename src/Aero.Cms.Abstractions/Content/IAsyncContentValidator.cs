using FluentValidation.Results;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Async validation rules that require database or service access.
/// </summary>
public interface IAsyncContentValidator
{
        /// <summary>
    /// ValidateAsync method.
    /// </summary>
Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        ContentItem item,
        ContentTypeDefinition type,
        CancellationToken ct);
}

/// <summary>
/// Marks the asynchronous validator that verifies stored references exist in the
/// authoritative site scope. Shared-field commands invoke this validator even though
/// the surrounding content variants are still incomplete drafts.
/// </summary>
public interface IContentReferenceExistenceValidator : IAsyncContentValidator;
