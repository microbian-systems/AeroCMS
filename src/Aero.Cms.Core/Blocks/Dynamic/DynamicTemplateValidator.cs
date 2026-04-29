using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Core;
using Aero.Core.Railway;
using Scriban;
using Scriban.Syntax;

namespace Aero.Cms.Core.Blocks.Dynamic;

public sealed partial class DynamicTemplateValidator
{
    private readonly SecureScribanTemplateOptions options;

    public DynamicTemplateValidator()
        : this(new SecureScribanTemplateOptions())
    {
    }

    public DynamicTemplateValidator(SecureScribanTemplateOptions options)
    {
        this.options = options;
    }

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
        public List<string> Errors { get; } = [];

        public override void Visit(ScriptFunction node)
        {
            Errors.Add("Template function declarations are not allowed.");
        }

        public override void Visit(ScriptImportStatement node)
        {
            Errors.Add("Template imports are not allowed.");
        }

        public override void Visit(ScriptFunctionCall node)
        {
            var functionName = GetFunctionName(node.Target);
            if (!IsAllowed(functionName))
            {
                Errors.Add($"Template function '{functionName}' is not allowed.");
            }

            base.Visit(node);
        }

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
}
