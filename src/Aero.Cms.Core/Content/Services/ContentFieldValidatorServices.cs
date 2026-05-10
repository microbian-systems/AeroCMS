using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using FluentValidation;

namespace Aero.Cms.Core.Content.Services;

public sealed class TextFieldValidator : IContentFieldValidator
{
    public string FieldType => "text";

    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be text.");
            return;
        }

        var value = element.GetString() ?? "";

        if (field.Settings.TryGetValue("maxLength", out var maxObj) && maxObj is JsonElement maxElem && maxElem.TryGetInt32(out var max) && value.Length > max)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be {max} characters or fewer.");

        if (field.Settings.TryGetValue("minLength", out var minObj) && minObj is JsonElement minElem && minElem.TryGetInt32(out var min) && value.Length < min)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be at least {min} characters.");
    }
}

public sealed class NumberFieldValidator : IContentFieldValidator
{
    public string FieldType => "number";

    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (!element.TryGetDecimal(out var value))
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a number.");
            return;
        }

        if (field.Settings.TryGetValue("min", out var minObj) && minObj is JsonElement minElem && minElem.TryGetDecimal(out var min) && value < min)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be at least {min}.");

        if (field.Settings.TryGetValue("max", out var maxObj) && maxObj is JsonElement maxElem && maxElem.TryGetDecimal(out var max) && value > max)
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be at most {max}.");
    }
}

public sealed class ReferenceFieldValidator : IContentFieldValidator
{
    public string FieldType => "reference";

    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        var targetContentType = field.Settings.TryGetValue("targetContentType", out var t) ? t?.ToString() : null;

        if (field.Settings.TryGetValue("allowMultiple", out var multi) && multi?.ToString() == "True")
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
