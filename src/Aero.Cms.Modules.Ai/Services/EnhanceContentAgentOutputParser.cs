using System.Text;
using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

internal static class EnhanceContentAgentOutputParser
{
    /// <summary>
    /// Strips markdown code fences and extracts the first JSON-looking object from the text.
    /// </summary>
    internal static string ExtractJsonObject(string text)
    {
        var span = text.AsSpan().Trim();

        if (span.StartsWith("```"))
        {
            var newline = span.IndexOf('\n');
            if (newline > 0)
            {
                span = span[(newline + 1)..];
            }
        }

        var closingFence = FindOuterClosingFence(span);
        if (closingFence >= 0)
        {
            span = span[..closingFence];
        }

        span = span.Trim();

        var openBrace = span.IndexOf('{');
        var closeBrace = span.LastIndexOf('}');

        if (openBrace >= 0 && closeBrace > openBrace)
        {
            span = span[openBrace..(closeBrace + 1)];
        }

        return span.ToString();
    }

    internal static EnhanceContentAgentOutput? Deserialize(
        string text,
        JsonSerializerOptions jsonOptions)
    {
        var cleaned = ExtractJsonObject(text);

        try
        {
            return JsonSerializer.Deserialize<EnhanceContentAgentOutput>(cleaned, jsonOptions);
        }
        catch (JsonException ex) when (!ex.Message.Contains("end of data", StringComparison.OrdinalIgnoreCase))
        {
            return TryDeserializeRelaxed(cleaned, jsonOptions);
        }
    }

    private static int FindOuterClosingFence(ReadOnlySpan<char> span)
    {
        var trimmed = span.TrimEnd();
        if (trimmed.EndsWith("```"))
        {
            return span.Length - (span.Length - trimmed.Length) - 3;
        }

        return -1;
    }

    private static EnhanceContentAgentOutput? TryDeserializeRelaxed(
        string text,
        JsonSerializerOptions jsonOptions)
    {
        if (!TryReadJsonStringProperty(text, "enhancedText", jsonOptions, out var enhancedText))
        {
            return null;
        }

        _ = TryReadJsonStringProperty(text, "rationale", jsonOptions, out var rationale);
        var warnings = TryReadWarnings(text, jsonOptions);

        return new EnhanceContentAgentOutput(enhancedText, rationale, warnings);
    }

    private static bool TryReadJsonStringProperty(
        string text,
        string propertyName,
        JsonSerializerOptions jsonOptions,
        out string value)
    {
        value = string.Empty;

        var valueStart = FindPropertyValueStart(text, propertyName);
        if (valueStart < 0)
        {
            return false;
        }

        valueStart = SkipWhiteSpace(text, valueStart);
        if (valueStart >= text.Length)
        {
            return false;
        }

        if (StartsWithNull(text, valueStart))
        {
            return true;
        }

        if (text[valueStart] != '"')
        {
            return false;
        }

        var jsonString = new StringBuilder();
        jsonString.Append('"');

        for (var i = valueStart + 1; i < text.Length; i++)
        {
            var current = text[i];

            if (current == '\\')
            {
                jsonString.Append(current);
                if (i + 1 < text.Length)
                {
                    jsonString.Append(text[++i]);
                }

                continue;
            }

            if (current == '"' && LooksLikePropertyValueEnd(text, i + 1))
            {
                jsonString.Append('"');
                value = JsonSerializer.Deserialize<string>(jsonString.ToString(), jsonOptions) ?? string.Empty;
                return true;
            }

            AppendAsJsonStringContent(jsonString, current);
        }

        return false;
    }

    private static int FindPropertyValueStart(string text, string propertyName)
    {
        var pattern = $"\"{propertyName}\"";
        var propertyIndex = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (propertyIndex < 0)
        {
            return -1;
        }

        var colonIndex = text.IndexOf(':', propertyIndex + pattern.Length);
        return colonIndex < 0 ? -1 : colonIndex + 1;
    }

    private static bool LooksLikePropertyValueEnd(string text, int index)
    {
        index = SkipWhiteSpace(text, index);
        if (index >= text.Length)
        {
            return true;
        }

        if (text[index] is ',' or '}')
        {
            return true;
        }

        return text[index] == '"'
               && (StartsWithPropertyName(text, index, "rationale")
                   || StartsWithPropertyName(text, index, "warnings"));
    }

    private static IReadOnlyList<string> TryReadWarnings(
        string text,
        JsonSerializerOptions jsonOptions)
    {
        var valueStart = FindPropertyValueStart(text, "warnings");
        if (valueStart < 0)
        {
            return [];
        }

        valueStart = SkipWhiteSpace(text, valueStart);
        if (valueStart >= text.Length || text[valueStart] != '[')
        {
            return [];
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = valueStart; i < text.Length; i++)
        {
            var current = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '[')
            {
                depth++;
            }
            else if (current == ']')
            {
                depth--;
                if (depth == 0)
                {
                    var arrayJson = text[valueStart..(i + 1)];
                    return JsonSerializer.Deserialize<IReadOnlyList<string>>(arrayJson, jsonOptions) ?? [];
                }
            }
        }

        return [];
    }

    private static int SkipWhiteSpace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool StartsWithNull(string text, int index)
    {
        return text.AsSpan(index).StartsWith("null", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithPropertyName(string text, int index, string propertyName)
    {
        return text.AsSpan(index).StartsWith($"\"{propertyName}\"", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendAsJsonStringContent(StringBuilder builder, char current)
    {
        switch (current)
        {
            case '\n':
                builder.Append("\\n");
                break;
            case '\r':
                builder.Append("\\r");
                break;
            case '\t':
                builder.Append("\\t");
                break;
            case '\b':
                builder.Append("\\b");
                break;
            case '\f':
                builder.Append("\\f");
                break;
            default:
                if (char.IsControl(current))
                {
                    builder.Append("\\u");
                    builder.Append(((int)current).ToString("x4"));
                }
                else
                {
                    builder.Append(current);
                }

                break;
        }
    }
}
