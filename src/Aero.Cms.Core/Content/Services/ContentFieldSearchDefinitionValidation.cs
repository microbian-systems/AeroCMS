using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Validates provider-neutral indexing and search capabilities selected for content fields.
/// </summary>
internal static class ContentFieldSearchDefinitionValidator
{
    private static readonly HashSet<string> FullTextFieldTypes =
    [
        ContentFieldTypes.Text,
        ContentFieldTypes.RichText,
        ContentFieldTypes.Url,
        ContentFieldTypes.List,
        ContentFieldTypes.Dictionary
    ];

    private static readonly HashSet<string> SemanticFieldTypes =
    [
        ContentFieldTypes.Text,
        ContentFieldTypes.RichText
    ];

    public static Result<NoneType, AeroError> Validate(
        IEnumerable<ContentFieldDefinition> fields)
    {
        var errors = new List<string>();
        foreach (var field in fields)
        {
            var label = string.IsNullOrWhiteSpace(field.Label)
                ? field.Name
                : field.Label;

            if (field.FullTextSearchable
                && !FullTextFieldTypes.Contains(field.FieldType))
            {
                errors.Add(
                    $"Field '{label}' cannot use full-text search because '{field.FieldType}' is not a textual field type.");
            }

            if (field.SemanticSearchable
                && !SemanticFieldTypes.Contains(field.FieldType))
            {
                errors.Add(
                    $"Field '{label}' cannot use semantic search because embeddings require short or rich text.");
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<NoneType, AeroError>(Prelude.None)
            : AeroError.ValidationError(errors);
    }
}
