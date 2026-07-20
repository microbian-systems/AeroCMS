using System.Text;
using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Extracts and deserializes structured enhancement output from provider-generated text.
/// </summary>
/// <remarks>
/// The relaxed fallback is a recovery heuristic for imperfect JSON; it is not a general JSON parser
/// and does not validate the meaning, safety, or provenance of provider output.
/// </remarks>
internal static class EnhanceContentAgentOutputParser
{
    /// <summary>
    /// Removes an outer markdown fence when recognizable and selects the broadest brace-delimited region.
    /// </summary>
    /// <param name="text">The provider-generated text to inspect.</param>
    /// <returns>
    /// Trimmed text between the first opening brace and last closing brace when both are present;
    /// otherwise, the trimmed, optionally unfenced input.
    /// </returns>
    /// <remarks>
    /// The method does not match nested objects or quoted braces and can return a region containing
    /// more than one object. Callers must still deserialize and validate the result.
    /// </remarks>
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

    /// <summary>
    /// Deserializes provider text, using a limited relaxed parser after non-truncation JSON failures.
    /// </summary>
    /// <param name="text">The provider-generated text.</param>
    /// <param name="jsonOptions">Serializer options used for strict and relaxed value parsing.</param>
    /// <returns>The parsed output, or <see langword="null"/> when relaxed recovery cannot find enhanced text.</returns>
    /// <exception cref="JsonException">
    /// Strict parsing reports an end-of-data error, or relaxed parsing encounters an invalid recoverable value.
    /// </exception>
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

    /// <summary>
    /// Locates a closing triple-backtick fence at the trimmed end of a span.
    /// </summary>
    /// <param name="span">The candidate fenced content.</param>
    /// <returns>The fence's index in <paramref name="span"/>, or <c>-1</c> when no terminal fence exists.</returns>
    private static int FindOuterClosingFence(ReadOnlySpan<char> span)
    {
        var trimmed = span.TrimEnd();
        if (trimmed.EndsWith("```"))
        {
            return span.Length - (span.Length - trimmed.Length) - 3;
        }

        return -1;
    }

    /// <summary>
    /// Recovers expected properties from text that failed strict object deserialization.
    /// </summary>
    /// <param name="text">The extracted JSON-like text.</param>
    /// <param name="jsonOptions">Serializer options used to decode recovered strings and warnings.</param>
    /// <returns>A recovered output when <c>enhancedText</c> can be read; otherwise, <see langword="null"/>.</returns>
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

    /// <summary>
    /// Attempts to recover a named JSON string property while tolerating unescaped content.
    /// </summary>
    /// <param name="text">The JSON-like source text.</param>
    /// <param name="propertyName">The property name to find without regard to case.</param>
    /// <param name="jsonOptions">Serializer options used to decode the reconstructed JSON string.</param>
    /// <param name="value">Receives the decoded string, or an empty string for a JSON null value.</param>
    /// <returns><see langword="true"/> when a string or null property can be recovered; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Finds the first colon following a case-insensitive quoted property name.
    /// </summary>
    /// <param name="text">The JSON-like source text.</param>
    /// <param name="propertyName">The property name to find.</param>
    /// <returns>The index after the colon, or <c>-1</c> when the property or colon is absent.</returns>
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

    /// <summary>
    /// Determines whether a quote is followed by a recognized string-property boundary.
    /// </summary>
    /// <param name="text">The JSON-like source text.</param>
    /// <param name="index">The position immediately after the candidate closing quote.</param>
    /// <returns>
    /// <see langword="true"/> at end of input, before a comma or closing brace, or before the
    /// recognized <c>rationale</c> or <c>warnings</c> properties.
    /// </returns>
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

    /// <summary>
    /// Attempts to extract a balanced JSON array from the <c>warnings</c> property.
    /// </summary>
    /// <param name="text">The JSON-like source text.</param>
    /// <param name="jsonOptions">Serializer options used to deserialize the array.</param>
    /// <returns>The warnings array, or an empty collection when a complete array is not found or deserializes to null.</returns>
    /// <exception cref="JsonException">The located array is not valid for a string collection.</exception>
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

    /// <summary>
    /// Advances an index past consecutive whitespace characters.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="index">The starting index.</param>
    /// <returns>The first non-whitespace index, or the text length.</returns>
    private static int SkipWhiteSpace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Tests whether the remaining text begins with the JSON null literal without regard to case.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <param name="index">The starting index.</param>
    /// <returns><see langword="true"/> when the remaining span starts with <c>null</c>.</returns>
    private static bool StartsWithNull(string text, int index)
    {
        return text.AsSpan(index).StartsWith("null", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests whether the remaining text begins with a quoted property name without regard to case.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <param name="index">The starting index.</param>
    /// <param name="propertyName">The unquoted property name.</param>
    /// <returns><see langword="true"/> when the quoted property name starts at <paramref name="index"/>.</returns>
    private static bool StartsWithPropertyName(string text, int index, string propertyName)
    {
        return text.AsSpan(index).StartsWith($"\"{propertyName}\"", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Appends one character in JSON string-content form.
    /// </summary>
    /// <param name="builder">The reconstructed JSON string.</param>
    /// <param name="current">The character to append or escape.</param>
    /// <remarks>
    /// Newlines, carriage returns, tabs, quotes, backspace, form feed, and remaining control
    /// characters are escaped; printable characters are appended unchanged.
    /// </remarks>
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
            case '"':
                builder.Append("\\\"");
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
