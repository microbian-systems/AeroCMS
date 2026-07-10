using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Core;
using Aero.Core.Railway;
using Scriban;
using Scriban.Syntax;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Represents a class for DynamicTemplateValidator.
/// </summary>
public sealed partial class DynamicTemplateValidator
{
    private readonly SecureScribanTemplateOptions options;

        /// <summary>
    /// Initializes a new instance of the <see cref="DynamicTemplateValidator"/> class.
    /// </summary>
public DynamicTemplateValidator()
        : this(new SecureScribanTemplateOptions())
    {
    }

        /// <summary>
    /// Initializes a new instance of the <see cref="DynamicTemplateValidator"/> class.
    /// </summary>
public DynamicTemplateValidator(SecureScribanTemplateOptions options)
    {
        this.options = options;
    }

        /// <summary>
    /// Validate method.
    /// </summary>
public Result<NoneType, AeroError> Validate(string template, JsonDocument? schema = null)
    {
        var errors = ValidateTemplateText(template);
        if (errors.Count > 0)
        {
            return AeroError.ValidationError(errors);
        }

        var parsed = Template.Parse(template);
        if (parsed.HasErrors)
        {
            return AeroError.ValidationError(parsed.Messages.Select(message => message.Message));
        }

        var securityVisitor = new ScribanSecurityVisitor(options);
        securityVisitor.Visit(parsed.Page);
        if (securityVisitor.Errors.Count > 0)
        {
            return AeroError.ValidationError(securityVisitor.Errors);
        }

        return Prelude.Ok<NoneType, AeroError>(Prelude.None);
    }

        /// <summary>
    /// ValidateData method.
    /// </summary>
public Result<NoneType, AeroError> ValidateData(JsonDocument? data, JsonDocument? schema)
    {
        if (schema is null)
            return Prelude.Ok<NoneType, AeroError>(Prelude.None);

        if (data is null || data.RootElement.ValueKind != JsonValueKind.Object)
            return AeroError.ValidationError(["Template data must be a JSON object."]);

        var errors = new List<string>();
        var schemaRoot = schema.RootElement;

        if (schemaRoot.TryGetProperty("required", out var required))
        {
            foreach (var requiredField in required.EnumerateArray())
            {
                var fieldName = requiredField.GetString();
                if (!string.IsNullOrWhiteSpace(fieldName) &&
                    !data.RootElement.TryGetProperty(fieldName, out _))
                {
                    errors.Add($"Required field '{fieldName}' is missing.");
                }
            }
        }

        if (schemaRoot.TryGetProperty("properties", out var properties))
        {
            foreach (var property in data.RootElement.EnumerateObject())
            {
                if (!properties.TryGetProperty(property.Name, out var propertySchema))
                {
                    errors.Add($"Field '{property.Name}' is not defined by the content type.");
                    continue;
                }

                var expectedType = propertySchema.GetProperty("type").GetString();
                if (!MatchesType(property.Value, expectedType))
                    errors.Add($"Field '{property.Name}' must be of type '{expectedType}'.");
            }
        }

        return errors.Count == 0
            ? Prelude.Ok<NoneType, AeroError>(Prelude.None)
            : AeroError.ValidationError(errors);
    }

    internal IReadOnlyList<string> ValidateTemplateText(string template)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(template))
        {
            errors.Add("Template content is required.");
            return errors;
        }

        var templateLength = Encoding.UTF8.GetByteCount(template);
        if (templateLength > options.MaxTemplateLengthBytes)
        {
            errors.Add($"Template content exceeds the {options.MaxTemplateLengthBytes} byte limit.");
        }

        if (template.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
            template.Contains("</script", StringComparison.OrdinalIgnoreCase) ||
            template.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Templates cannot contain script tags or javascript: URLs.");
        }

        if (EventHandlerAttributeRegex().IsMatch(template))
        {
            errors.Add("Templates cannot contain inline JavaScript event handler attributes.");
        }

        return errors;
    }

    [GeneratedRegex("\\son[a-zA-Z]+\\s*=", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EventHandlerAttributeRegex();

    private sealed class ScribanSecurityVisitor(SecureScribanTemplateOptions options) : ScriptVisitor
    {
                /// <summary>
        /// Gets or sets the Errors.
        /// </summary>
public List<string> Errors { get; } = [];

                /// <summary>
        /// Visit method.
        /// </summary>
public override void Visit(ScriptFunction node)
        {
            Errors.Add("Template function declarations are not allowed.");
        }

                /// <summary>
        /// Visit method.
        /// </summary>
public override void Visit(ScriptImportStatement node)
        {
            Errors.Add("Template imports are not allowed.");
        }

                /// <summary>
        /// Visit method.
        /// </summary>
public override void Visit(ScriptFunctionCall node)
        {
            var functionName = GetFunctionName(node.Target);
            if (!IsAllowed(functionName))
            {
                Errors.Add($"Template function '{functionName}' is not allowed.");
            }

            base.Visit(node);
        }

                /// <summary>
        /// Visit method.
        /// </summary>
public override void Visit(ScriptPipeCall node)
        {
            var functionName = GetFunctionName(node.To);
            if (!IsAllowed(functionName))
            {
                Errors.Add($"Template pipe function '{functionName}' is not allowed.");
            }

            base.Visit(node);
        }

        private bool IsAllowed(string functionName)
        {
            if (functionName.Contains('|', StringComparison.Ordinal))
            {
                return functionName
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .All(IsSingleFunctionAllowed);
            }

            return IsSingleFunctionAllowed(functionName);
        }

        private bool IsSingleFunctionAllowed(string functionName)
        {
            if (options.AllowAllFunctions)
            {
                return true;
            }

            if (options.AllowedFunctionNames.Contains(functionName))
            {
                return true;
            }

            return options.AllowedFunctionNames.Any(allowedFunction =>
                functionName.StartsWith(allowedFunction, StringComparison.OrdinalIgnoreCase) &&
                functionName.Length > allowedFunction.Length &&
                char.IsWhiteSpace(functionName[allowedFunction.Length]));
        }

        private static string GetFunctionName(ScriptExpression? expression)
        {
            if (expression is ScriptFunctionCall functionCall)
            {
                return GetFunctionName(functionCall.Target);
            }

            return expression?.ToString()?.Trim() is { Length: > 0 } functionName
                ? functionName
                : "<unknown>";
        }
    }

    private static bool MatchesType(JsonElement value, string? expectedType) => expectedType switch
    {
        "string" => value.ValueKind is JsonValueKind.String or JsonValueKind.Null,
        "number" => value.ValueKind is JsonValueKind.Number or JsonValueKind.Null,
        "integer" => value.ValueKind is JsonValueKind.Number or JsonValueKind.Null,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null,
        "array" => value.ValueKind is JsonValueKind.Array or JsonValueKind.Null,
        "object" => value.ValueKind is JsonValueKind.Object or JsonValueKind.Null,
        _ => true
    };
}
