using System.Text.Json;
using System.Text.RegularExpressions;
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

/// <summary>Validates inclusive, whole-number range values.</summary>
public sealed class RangeFieldValidator : IContentFieldValidator
{
    /// <inheritdoc />
    public string FieldType => ContentFieldTypes.Range;

    /// <inheritdoc />
    public void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        if (!element.TryGetInt32(out var value))
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} must be a whole number.");
            return;
        }

        if (field.Settings.TryGetValue(
                RangeContentFieldSettings.Start,
                out var startElement)
            && startElement.TryGetInt32(out var start)
            && value < start)
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} must be at least {start}.");
        }

        if (field.Settings.TryGetValue(
                RangeContentFieldSettings.End,
                out var endElement)
            && endElement.TryGetInt32(out var end)
            && value > end)
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} must be at most {end}.");
        }

        var allowNegative =
            field.Settings.TryGetValue(
                RangeContentFieldSettings.AllowNegative,
                out var negativeElement)
            && negativeElement.ValueKind == JsonValueKind.True;
        if (!allowNegative && value < 0)
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} cannot be negative.");
        }
    }
}

/// <summary>Validates the hexadecimal string emitted by <c>RadzenColorPicker</c>.</summary>
public sealed partial class ColorFieldValidator : IContentFieldValidator
{
    /// <inheritdoc />
    public string FieldType => ContentFieldTypes.Color;

    /// <inheritdoc />
    public void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} must be a color.");
            return;
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            if (field.Required && mode == ContentValidationMode.Publish)
            {
                context.AddFailure(
                    field.Name,
                    $"{field.Label ?? field.Name} is required.");
            }

            return;
        }

        if (!IsSupportedColor(value))
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} must be a six- or eight-digit hexadecimal color.");
        }
    }

    internal static bool IsSupportedColor(string value) =>
        HexColorPattern().IsMatch(value.Trim());

    [GeneratedRegex(
        "^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();
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
    /// Target content-type IDs are resolved by the asynchronous validator.
    /// </remarks>
    public void ValidateElement(ContentFieldDefinition field, JsonElement element, ContentValidationMode mode, ValidationContext<ContentItem> context)
    {
        if (IsCmsDocumentReference(field))
        {
            ValidateCmsDocumentReference(field, element, mode, context);
            return;
        }

        var isRequired = field.Required && mode == ContentValidationMode.Publish;

        if (field.Settings.TryGetValue("allowMultiple", out var multiple)
            && multiple.ValueKind == JsonValueKind.True)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a list of references.");
                return;
            }

            var items = element.EnumerateArray().ToArray();
            if (items.Length == 0)
            {
                if (isRequired)
                    context.AddFailure(field.Name, $"{field.Label ?? field.Name} is required.");
                return;
            }

            foreach (var item in items)
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
            if (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString()))
            {
                if (isRequired)
                    context.AddFailure(field.Name, $"{field.Label ?? field.Name} is required.");
                return;
            }

            if (element.ValueKind != JsonValueKind.String || !long.TryParse(element.GetString(), out _))
                context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a valid reference ID.");
        }
    }

    private static void ValidateCmsDocumentReference(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            if (field.Required && mode == ContentValidationMode.Publish)
                context.AddFailure(field.Name, $"{field.Label ?? field.Name} is required.");
            return;
        }

        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("source", out var sourceElement)
            || sourceElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(sourceElement.GetString())
            || !element.TryGetProperty("id", out var idElement)
            || idElement.ValueKind != JsonValueKind.String
            || !long.TryParse(
                idElement.GetString(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var id)
            || id <= 0)
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} must select a valid page, post, or documentation item.");
            return;
        }

        var source = sourceElement.GetString()!;
        var allowedSources = GetAllowedSources(field);
        if (!CmsContentReferenceSources.IsSupportedSource(source)
            || (allowedSources.Count > 0
                && !CmsContentReferenceSources.IsAllowedSource(
                    source,
                    allowedSources)))
        {
            context.AddFailure(
                field.Name,
                $"{field.Label ?? field.Name} uses an unsupported content source.");
        }
    }

    internal static bool IsCmsDocumentReference(ContentFieldDefinition field) =>
        field.Settings.TryGetValue(
            ReferenceContentFieldSettings.TargetKind,
            out var targetKind)
        && targetKind.ValueKind == JsonValueKind.String
        && string.Equals(
            targetKind.GetString(),
            ReferenceContentFieldSettings.TargetKindCmsDocument,
            StringComparison.Ordinal);

    internal static IReadOnlyList<string> GetAllowedSources(
        ContentFieldDefinition field) =>
        field.Settings.TryGetValue(
            ReferenceContentFieldSettings.AllowedSources,
            out var sources)
        && sources.ValueKind == JsonValueKind.Array
            ? sources.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray()
            : [];
}
