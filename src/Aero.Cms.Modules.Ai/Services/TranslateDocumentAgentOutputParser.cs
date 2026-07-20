using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Extracts and deserializes structured translation output from provider-generated text.
/// </summary>
internal static class TranslateDocumentAgentOutputParser
{
    /// <summary>
    /// Removes recognizable surrounding text and deserializes the translation response.
    /// </summary>
    /// <param name="text">The provider-generated text.</param>
    /// <param name="jsonOptions">Serializer options used to deserialize the extracted object.</param>
    /// <returns>The parsed output, or <see langword="null"/> when the JSON literal is null.</returns>
    /// <exception cref="JsonException">The extracted text is not valid for the expected response shape.</exception>
    internal static TranslateDocumentAgentOutput? Deserialize(
        string text,
        JsonSerializerOptions jsonOptions)
    {
        var cleaned = EnhanceContentAgentOutputParser.ExtractJsonObject(text);
        return JsonSerializer.Deserialize<TranslateDocumentAgentOutput>(cleaned, jsonOptions);
    }
}
