using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Core;
using Aero.Core.Railway;
using Scriban;
using Scriban.Syntax;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Validates Content Type Scriban templates and their input data.
/// </summary>
public sealed partial class ScribanTemplateValidator
{
    private readonly SecureScribanTemplateOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScribanTemplateValidator"/> class.
    /// </summary>
    /// <remarks>Uses a new instance of the default guardrails.</remarks>
    public ScribanTemplateValidator()
        : this(new SecureScribanTemplateOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScribanTemplateValidator"/> class.
    /// </summary>
    /// <param name="options">The template validation limits.</param>
    public ScribanTemplateValidator(SecureScribanTemplateOptions options)
    {
        this.options = options;
    }

    /// <summary>
    /// Validates template text, Scriban syntax, and prohibited function calls.
    /// </summary>
    /// <param name="template">The template text to validate.</param>
    /// <param name="schema">
    /// Reserved for schema-aware template validation; the current implementation does not
    /// inspect this parameter.
    /// </param>
    /// <returns>A successful result when validation passes; otherwise all detected errors.</returns>
    /// <remarks>
    /// Validation enforces the UTF-8 byte limit, rejects script markup, JavaScript URLs,
    /// inline event-handler attributes, includes, and dynamic evaluation, and reports Scriban
    /// parse errors. It is not a substitute for output sanitization.
    /// </remarks>
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

        var securityVisitor = new ScribanSecurityVisitor();
        securityVisitor.Visit(parsed.Page);
        if (securityVisitor.Errors.Count > 0)
        {
            return AeroError.ValidationError(securityVisitor.Errors);
        }

        return Prelude.Ok<NoneType, AeroError>(Prelude.None);
    }

    /// <summary>
    /// Performs shallow top-level validation of template data against a generated schema.
    /// </summary>
    /// <param name="data">The JSON value supplied as the template's field data.</param>
    /// <param name="schema">The caller-owned schema to inspect, or <see langword="null"/> to skip validation.</param>
    /// <returns>A successful result when the shallow checks pass; otherwise aggregated validation errors.</returns>
    /// <remarks>
    /// The method checks that data is an object, required top-level names exist, supplied
    /// names occur in <c>properties</c>, and top-level JSON kinds match each property's
    /// <c>type</c>. It does not recursively validate nested values or enforce other JSON Schema
    /// keywords. JSON null is accepted for every recognized type, and <c>integer</c> checks only
    /// for a JSON number rather than mathematical integrality. The method expects the generated
    /// schema shape; malformed <c>required</c>, <c>properties</c>, or property <c>type</c>
    /// members may cause JSON access exceptions instead of validation results.
    /// </remarks>
    public Result<NoneType, AeroError> ValidateData(JsonElement data, JsonDocument? schema)
    {
        if (schema is null)
            return Prelude.Ok<NoneType, AeroError>(Prelude.None);

        if (data.ValueKind != JsonValueKind.Object)
            return AeroError.ValidationError(["Template data must be a JSON object."]);

        var errors = new List<string>();
        var schemaRoot = schema.RootElement;

        if (schemaRoot.TryGetProperty("required", out var required))
        {
            foreach (var requiredField in required.EnumerateArray())
            {
                var fieldName = requiredField.GetString();
                if (!string.IsNullOrWhiteSpace(fieldName) &&
                    !data.TryGetProperty(fieldName, out _))
                {
                    errors.Add($"Required field '{fieldName}' is missing.");
                }
            }
        }

        if (schemaRoot.TryGetProperty("properties", out var properties))
        {
            foreach (var property in data.EnumerateObject())
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

    private sealed class ScribanSecurityVisitor : ScriptVisitor
    {
        public List<string> Errors { get; } = [];

        public override void Visit(ScriptFunctionCall node)
        {
            var functionName = GetFunctionName(node.Target);
            if (IsDisallowedFunction(functionName, out var error))
            {
                Errors.Add(error);
            }

            base.Visit(node);
        }

        public override void Visit(ScriptPipeCall node)
        {
            var functionName = GetFunctionName(node.To);
            if (IsDisallowedFunction(functionName, out var error))
            {
                Errors.Add(error);
            }

            base.Visit(node);
        }

        private static bool IsInclude(string functionName) =>
            functionName.Equals("include", StringComparison.OrdinalIgnoreCase) ||
            functionName.StartsWith("include ", StringComparison.OrdinalIgnoreCase);

        private static bool IsDisallowedFunction(string functionName, out string error)
        {
            if (IsInclude(functionName))
            {
                error =
                    "Template includes are not supported. Use local functions or an explicitly supplied import scope.";
                return true;
            }

            if (functionName.Equals("object.eval", StringComparison.OrdinalIgnoreCase) ||
                functionName.Equals("object.eval_template", StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "Dynamic template evaluation is not supported. Template code must be validated before rendering.";
                return true;
            }

            error = string.Empty;
            return false;
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
