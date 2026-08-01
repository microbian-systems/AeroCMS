using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>Validates settings and scalar defaults for bounded Range and Color fields.</summary>
internal static class ScalarContentFieldDefinitionValidator
{
    public static Result<NoneType, AeroError> Validate(
        IEnumerable<ContentFieldDefinition> fields)
    {
        var errors = new List<string>();
        foreach (var field in fields)
        {
            switch (field.FieldType)
            {
                case ContentFieldTypes.Range:
                    ValidateRange(field, errors);
                    break;
                case ContentFieldTypes.Color:
                    ValidateColor(field, errors);
                    break;
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<NoneType, AeroError>(Prelude.None)
            : AeroError.ValidationError(errors);
    }

    private static void ValidateRange(
        ContentFieldDefinition field,
        ICollection<string> errors)
    {
        if (!TryGetRequiredInteger(field, RangeContentFieldSettings.Start, out var start)
            || !TryGetRequiredInteger(field, RangeContentFieldSettings.End, out var end))
        {
            errors.Add(
                $"{Label(field)} must define whole-number Start with and End with values.");
            return;
        }

        if (start > end)
        {
            errors.Add($"{Label(field)} Start with value cannot exceed its End with value.");
            return;
        }

        var allowNegative = GetBoolean(
            field,
            RangeContentFieldSettings.AllowNegative);
        if (!allowNegative && start < 0)
        {
            errors.Add(
                $"{Label(field)} cannot start below zero unless negative values are allowed.");
        }

        if (string.IsNullOrWhiteSpace(field.DefaultValue))
        {
            return;
        }

        if (!int.TryParse(
                field.DefaultValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var defaultValue)
            || defaultValue < start
            || defaultValue > end)
        {
            errors.Add(
                $"{Label(field)} default value must be a whole number between {start} and {end}.");
        }
    }

    private static void ValidateColor(
        ContentFieldDefinition field,
        ICollection<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(field.DefaultValue)
            && !ColorFieldValidator.IsSupportedColor(field.DefaultValue))
        {
            errors.Add(
                $"{Label(field)} default value must be a six- or eight-digit hexadecimal color.");
        }
    }

    private static bool TryGetRequiredInteger(
        ContentFieldDefinition field,
        string key,
        out int value)
    {
        value = default;
        return field.Settings.TryGetValue(key, out var setting)
               && setting.TryGetInt32(out value);
    }

    private static bool GetBoolean(
        ContentFieldDefinition field,
        string key) =>
        field.Settings.TryGetValue(key, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.True;

    private static string Label(ContentFieldDefinition field) =>
        field.Label ?? field.Name;
}
