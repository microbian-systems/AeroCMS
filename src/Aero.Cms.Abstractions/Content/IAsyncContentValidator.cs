using FluentValidation;
using FluentValidation.Results;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Async validation rules that require database or service access.
/// </summary>
public interface IAsyncContentValidator
{
    Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        ContentItem item,
        ContentTypeDefinition type,
        CancellationToken ct);
}
