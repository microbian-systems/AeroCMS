using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using FluentValidation;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Validates text field JSON values and configured length limits.
/// </summary>
public sealed class TextFieldValidator : IContentFieldValidator
{
    /// <inheritdoc />
    public string FieldType => "text";

    /// <inheritdoc />
    /// <remarks>
    /// Requires a JSON string. Integer <c>minLength</c> and <c>maxLength</c> settings are
    /// enforced in both validation modes when present.
    /// </remarks>
    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be text.");
            return;
        }

        var value = element.GetString() ?? "";

        if (field.Settings.TryGetValue("maxLength", out var maxElement) && maxElement.TryGetInt32(out var max) && value.Length > max)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be {max} characters or fewer.");

        if (field.Settings.TryGetValue("minLength", out var minElement) && minElement.TryGetInt32(out var min) && value.Length < min)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be at least {min} characters.");
    }
}

/// <summary>
/// Validates numeric field JSON values and configured decimal bounds.
/// </summary>
public sealed class NumberFieldValidator : IContentFieldValidator
{
    /// <inheritdoc />
    public string FieldType => "number";

    /// <inheritdoc />
    /// <remarks>
    /// Requires a JSON number representable as <see cref="decimal"/>. Decimal <c>min</c> and
    /// <c>max</c> settings are enforced in both validation modes when present.
    /// </remarks>
    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (!element.TryGetDecimal(out var value))
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a number.");
            return;
        }

        if (field.Settings.TryGetValue("min", out var minElement) && minElement.TryGetDecimal(out var min) && value < min)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be at least {min}.");

        if (field.Settings.TryGetValue("max", out var maxElement) && maxElement.TryGetDecimal(out var max) && value > max)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be at most {max}.");
    }
}

/// <summary>
/// Validates the JSON shape and identifier syntax of reference fields.
/// </summary>
public sealed class ReferenceFieldValidator : IContentFieldValidator
{
    /// <inheritdoc />
    public string FieldType => "reference";

    /// <inheritdoc />
    /// <remarks>
    /// When <c>allowMultiple</c> is JSON <see langword="true"/>, the value must be an array
    /// of strings parseable as <see cref="long"/>. Otherwise one parseable string is required.
    /// The optional <c>targetContentType</c> setting is read but is not enforced.
    /// </remarks>
    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        var targetContentType = field.Settings.TryGetValue("targetContentType", out var target)
            && target.ValueKind == JsonValueKind.String
                ? target.GetString()
                : null;

        if (field.Settings.TryGetValue("allowMultiple", out var multiple)
            && multiple.ValueKind == JsonValueKind.True)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a list of references.");
                return;
            }

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || !long.TryParse(item.GetString(), out _))
                {
                    context.AddFailure(field.Name, $"{field.Label ?? field.Name} contains invalid reference IDs.");
                    break;
                }
            }
        }
        else
        {
            if (element.ValueKind != JsonValueKind.String || !long.TryParse(element.GetString(), out _))
                context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a valid reference ID.");
        }
    }
}
