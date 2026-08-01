using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using FluentValidation;

namespace Aero.Cms.Core.Content.Services;

internal static class CompositeContentFieldDefinitionValidator
{
    internal const int MaximumAllowedValues = 100;
    internal const int MaximumItems = 50;
    internal const int MaximumEntries = 50;

    public static Result<NoneType, AeroError> Validate(IEnumerable<ContentFieldDefinition> fields)
    {
        var errors = new List<string>();
        foreach (var field in fields.Where(IsComposite))
        {
            if (!string.IsNullOrWhiteSpace(field.DefaultValue))
                errors.Add($"{Label(field)} does not support a scalar default value.");

            switch (field.FieldType)
            {
                case ContentFieldTypes.List:
                    ValidateListDefinition(field, errors);
                    break;
                case ContentFieldTypes.Gallery:
                    ValidateBounds(field, CompositeContentFieldSettings.MinimumItems, CompositeContentFieldSettings.MaximumItems, MaximumItems, errors);
                    break;
                case ContentFieldTypes.Dictionary:
                    ValidateScalarType(field, CompositeContentFieldSettings.ValueType, errors);
                    ValidateBounds(field, CompositeContentFieldSettings.MinimumEntries, CompositeContentFieldSettings.MaximumEntries, MaximumEntries, errors);
                    break;
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<NoneType, AeroError>(Prelude.None)
            : AeroError.ValidationError(errors);
    }

    private static void ValidateListDefinition(ContentFieldDefinition field, List<string> errors)
    {
        var scalarType = ValidateScalarType(field, CompositeContentFieldSettings.ItemType, errors);
        IReadOnlyList<string> allowed = [];
        if (!field.Settings.TryGetValue(CompositeContentFieldSettings.AllowedValues, out var allowedSetting)
            || allowedSetting.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{Label(field)} must define allowed values as an array of non-empty strings.");
        }
        else if (allowedSetting.EnumerateArray().Any(item =>
                     item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
        {
            errors.Add($"{Label(field)} allowed values must all be non-empty strings.");
        }
        else
        {
            allowed = allowedSetting.EnumerateArray().Select(item => item.GetString()!).ToArray();
        }
        if (allowed.Count == 0)
            errors.Add($"{Label(field)} must define at least one allowed value.");
        if (allowed.Count > MaximumAllowedValues)
            errors.Add($"{Label(field)} cannot define more than {MaximumAllowedValues} allowed values.");
        var effectiveAllowedCount = allowed.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (scalarType == CompositeContentFieldSettings.Number)
        {
            var numbers = new List<decimal>(allowed.Count);
            foreach (var value in allowed)
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    errors.Add($"{Label(field)} allowed values must all be invariant numbers.");
                    numbers.Clear();
                    break;
                }

                numbers.Add(number);
            }

            if (numbers.Count > 0)
                effectiveAllowedCount = numbers.Distinct().Count();
        }

        if (effectiveAllowedCount != allowed.Count)
            errors.Add($"{Label(field)} allowed values must be unique.");

        ValidateBounds(field, CompositeContentFieldSettings.MinimumItems, CompositeContentFieldSettings.MaximumItems, MaximumItems, errors);
        var minimum = GetInt(field, CompositeContentFieldSettings.MinimumItems, 0);
        if (minimum > effectiveAllowedCount)
            errors.Add($"{Label(field)} cannot require more selections than its {effectiveAllowedCount} unique allowed values.");
    }

    private static string? ValidateScalarType(ContentFieldDefinition field, string key, List<string> errors)
    {
        var value = GetString(field, key);
        if (value is not (CompositeContentFieldSettings.Text or CompositeContentFieldSettings.Number))
            errors.Add($"{Label(field)} must use Short text or Number values.");
        return value;
    }

    private static void ValidateBounds(ContentFieldDefinition field, string minimumKey, string maximumKey, int ceiling, List<string> errors)
    {
        var minimum = 0;
        var valid = true;
        if (field.Settings.TryGetValue(minimumKey, out var minimumSetting)
            && !minimumSetting.TryGetInt32(out minimum))
        {
            errors.Add($"{Label(field)} minimum must be a whole number.");
            valid = false;
        }

        if (!field.Settings.TryGetValue(maximumKey, out var maximumSetting)
            || !maximumSetting.TryGetInt32(out var maximum))
        {
            errors.Add($"{Label(field)} maximum is required and must be a whole number.");
            return;
        }

        if (!valid) return;
        if (minimum < 0 || maximum < 1 || maximum > ceiling || minimum > maximum)
            errors.Add($"{Label(field)} limits must be between 0 and {ceiling}, with the minimum no greater than the maximum.");
    }

    internal static bool IsComposite(ContentFieldDefinition field) =>
        field.FieldType is ContentFieldTypes.List or ContentFieldTypes.Gallery or ContentFieldTypes.Dictionary;

    internal static string Label(ContentFieldDefinition field) => field.Label ?? field.Name;

    internal static string? GetString(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    internal static int GetInt(ContentFieldDefinition field, string key, int fallback) =>
        field.Settings.TryGetValue(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

    internal static IReadOnlyList<string> GetAllowedValues(ContentFieldDefinition field) =>
        field.Settings.TryGetValue(CompositeContentFieldSettings.AllowedValues, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray()
            : [];
}

public sealed class ListFieldValidator : IContentFieldValidator
{
    public string FieldType => ContentFieldTypes.List;

    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} must be a list.");
            return;
        }

        var values = element.EnumerateArray().ToArray();
        ValidateCount(field, values.Length, CompositeContentFieldSettings.MinimumItems, CompositeContentFieldSettings.MaximumItems, mode, context);
        var itemType = CompositeContentFieldDefinitionValidator.GetString(field, CompositeContentFieldSettings.ItemType);
        var allowed = CompositeContentFieldDefinitionValidator.GetAllowedValues(field);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            decimal? numericValue = null;
            string? normalized;
            if (itemType == CompositeContentFieldSettings.Number)
            {
                if (!value.TryGetDecimal(out var parsedNumber))
                {
                    context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} contains a value of the wrong type.");
                    return;
                }
                numericValue = parsedNumber;
                normalized = parsedNumber.ToString("G29", CultureInfo.InvariantCulture);
            }
            else
            {
                normalized = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            }

            if (normalized is null)
            {
                context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} contains a value of the wrong type.");
                return;
            }
            if (!seen.Add(normalized))
            {
                context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} cannot contain duplicate values.");
                return;
            }
            var isAllowed = itemType == CompositeContentFieldSettings.Number
                ? allowed.Any(candidate => decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed == numericValue)
                : allowed.Contains(normalized, StringComparer.Ordinal);
            if (!isAllowed)
            {
                context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} contains a value outside its allowed choices.");
                return;
            }
        }
    }

    internal static void ValidateCount(ContentFieldDefinition field, int count, string minimumKey, string maximumKey, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        // Empty composite values are valid while drafting, and remain valid
        // after publish when the field itself is optional. A configured
        // minimum applies only after an optional field has a value.
        if (count == 0
            && (mode == ContentValidationMode.Draft || !field.Required))
        {
            return;
        }

        var minimum = CompositeContentFieldDefinitionValidator.GetInt(field, minimumKey, 0);
        if (field.Required && mode == ContentValidationMode.Publish) minimum = Math.Max(1, minimum);
        var maximum = CompositeContentFieldDefinitionValidator.GetInt(field, maximumKey, CompositeContentFieldDefinitionValidator.MaximumItems);
        if (count < minimum || count > maximum)
            context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} must contain between {minimum} and {maximum} values.");
    }
}

public sealed class GalleryFieldValidator : IContentFieldValidator
{
    public string FieldType => ContentFieldTypes.Gallery;

    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} must be an image gallery.");
            return;
        }
        var values = element.EnumerateArray().ToArray();
        ListFieldValidator.ValidateCount(field, values.Length, CompositeContentFieldSettings.MinimumItems, CompositeContentFieldSettings.MaximumItems, mode, context);
        if (values.Any(value => value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())))
            context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} may contain only image URLs.");
    }
}

public sealed partial class DictionaryFieldValidator : IContentFieldValidator
{
    public string FieldType => ContentFieldTypes.Dictionary;

    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} must be a key/value object.");
            return;
        }

        var properties = element.EnumerateObject().ToArray();
        if (properties.Length == 0
            && (mode == ContentValidationMode.Draft || !field.Required))
        {
            return;
        }

        var minimum = CompositeContentFieldDefinitionValidator.GetInt(field, CompositeContentFieldSettings.MinimumEntries, 0);
        if (field.Required && mode == ContentValidationMode.Publish) minimum = Math.Max(1, minimum);
        var maximum = CompositeContentFieldDefinitionValidator.GetInt(field, CompositeContentFieldSettings.MaximumEntries, CompositeContentFieldDefinitionValidator.MaximumEntries);
        if (properties.Length < minimum || properties.Length > maximum)
            context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} must contain between {minimum} and {maximum} entries.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valueType = CompositeContentFieldDefinitionValidator.GetString(field, CompositeContentFieldSettings.ValueType);
        foreach (var property in properties)
        {
            if (!KeyPattern().IsMatch(property.Name) || !seen.Add(property.Name))
            {
                context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} contains an invalid or duplicate key.");
                return;
            }
            if (valueType == CompositeContentFieldSettings.Number
                ? property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDecimal(out _)
                : property.Value.ValueKind != JsonValueKind.String || (property.Value.GetString()?.Length ?? 0) > 1024)
            {
                context.AddFailure(field.Name, $"{CompositeContentFieldDefinitionValidator.Label(field)} contains a value of the wrong type.");
                return;
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9 _.-]{0,62}[A-Za-z0-9_.-])?$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
