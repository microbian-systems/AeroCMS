using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using FluentValidation;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Represents a class for TextFieldValidator.
/// </summary>
public sealed class TextFieldValidator : IContentFieldValidator
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "text";

        /// <summary>
    /// ValidateElement method.
    /// </summary>
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
/// Represents a class for NumberFieldValidator.
/// </summary>
public sealed class NumberFieldValidator : IContentFieldValidator
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "number";

        /// <summary>
    /// ValidateElement method.
    /// </summary>
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
/// Represents a class for ReferenceFieldValidator.
/// </summary>
public sealed class ReferenceFieldValidator : IContentFieldValidator
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "reference";

        /// <summary>
    /// ValidateElement method.
    /// </summary>
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
