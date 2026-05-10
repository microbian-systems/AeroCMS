using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;
using FluentValidation.Results;

namespace Aero.Cms.Core.Content.Services;

public sealed class UniqueSlugValidator(IContentService contentService) : IAsyncContentValidator
{
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

public sealed class ReferenceExistenceValidator(IContentService contentService) : IAsyncContentValidator
{
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(ContentItem item, ContentTypeDefinition type, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        foreach (var field in type.Fields.Where(f => f.FieldType == "reference"))
        {
            if (!item.Fields.TryGetValue(field.Name, out var element)) continue;
            if (element.ValueKind == System.Text.Json.JsonValueKind.Null) continue;

            if (field.Settings.TryGetValue("allowMultiple", out var multi) && multi?.ToString() == "True")
            {
                foreach (var refItem in element.EnumerateArray())
                    await CheckReference(refItem, field, failures, ct);
            }
            else
            {
                await CheckReference(element, field, failures, ct);
            }
        }

        return failures;
    }

    private async Task CheckReference(System.Text.Json.JsonElement element, ContentFieldDefinition field, List<ValidationFailure> failures, CancellationToken ct)
    {
        if (long.TryParse(element.GetString(), out var id) && !await contentService.ExistsAsync(id, ct))
            failures.Add(new ValidationFailure(field.Name, $"Referenced item '{id}' for '{field.Label ?? field.Name}' not found."));
    }
}
