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
