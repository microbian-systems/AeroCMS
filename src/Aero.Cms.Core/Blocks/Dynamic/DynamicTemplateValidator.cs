using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Core;
using Aero.Core.Railway;
using Scriban;

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
}
