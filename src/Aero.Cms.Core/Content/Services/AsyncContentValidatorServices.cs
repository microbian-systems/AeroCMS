using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation.Results;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Checks whether a site's slug is already assigned to a different content item.
/// </summary>
/// <remarks>
/// The check is site-wide and does not include content type or culture. It is an application-time
/// lookup and does not guarantee race-free uniqueness. Lookup failures represented as
/// <see cref="AeroError"/> values are treated as no conflict.
/// </remarks>
public sealed class UniqueSlugValidator(IContentService contentService) : IAsyncContentValidator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(ContentItem item, ContentTypeDefinition type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item.Slug))
            return [];

        var existingResult = await contentService.GetBySlugAsync(item.SiteId, item.Slug, ct);
        if (existingResult is Result<ContentItem, AeroError>.Ok ok && ok.Value.Id != item.Id)
            return [new ValidationFailure(nameof(item.Slug), $"Slug '{item.Slug}' is already in use.")];

        return [];
    }
}

/// <summary>
/// Verifies that parseable referenced content identifiers exist.
/// </summary>
/// <remarks>
/// Only fields whose type is exactly <c>reference</c> are inspected. The validator honors a
/// Boolean <c>allowMultiple</c> setting but does not verify the referenced item's site, content
/// type, or compatibility with any target schema. Non-parseable identifiers are expected to be
/// rejected by synchronous field validation and do not produce an existence failure here. When
/// invoked outside <see cref="ContentValidationService"/>, incorrectly shaped JSON may cause
/// <see cref="InvalidOperationException"/> while enumerating or reading reference values.
/// </remarks>
public sealed class ReferenceExistenceValidator(IContentService contentService) : IAsyncContentValidator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(ContentItem item, ContentTypeDefinition type, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        foreach (var field in type.Fields.Where(f => f.FieldType == "reference"))
        {
            if (!item.Fields.TryGetValue(field.Name, out var element)) continue;
            if (element.ValueKind == System.Text.Json.JsonValueKind.Null) continue;

            if (field.Settings.TryGetValue("allowMultiple", out var multiple)
                && multiple.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                foreach (var refItem in element.EnumerateArray())
                    await CheckReference(item.SiteId, refItem, field, failures, ct);
            }
            else
            {
                await CheckReference(item.SiteId, element, field, failures, ct);
            }
        }

        return failures;
    }

    private async Task CheckReference(long siteId, System.Text.Json.JsonElement element, ContentFieldDefinition field, List<ValidationFailure> failures, CancellationToken ct)
    {
        if (long.TryParse(element.GetString(), out var id) && !await contentService.ExistsAsync(siteId, id, ct))
            failures.Add(new ValidationFailure(field.Name, $"Referenced item '{id}' for '{field.Label ?? field.Name}' not found."));
    }
}
