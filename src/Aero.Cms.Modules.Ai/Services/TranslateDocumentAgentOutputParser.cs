using System.Text.Json;

namespace Aero.Cms.Modules.Ai.Services;

internal static class TranslateDocumentAgentOutputParser
{
    internal static TranslateDocumentAgentOutput? Deserialize(
        string text,
        JsonSerializerOptions jsonOptions)
    {
        var cleaned = EnhanceContentAgentOutputParser.ExtractJsonObject(text);
        return JsonSerializer.Deserialize<TranslateDocumentAgentOutput>(cleaned, jsonOptions);
    }
}
