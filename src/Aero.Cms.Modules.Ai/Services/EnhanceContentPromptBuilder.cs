using System.Text.Json;
using Aero.Cms.Abstractions.Ai;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Builds the user-role prompt used for a content-enhancement request.
/// </summary>
/// <remarks>
/// Request values are JSON-serialized into the prompt to preserve their structure. Serialization does
/// not sanitize the values or prevent prompt-injection instructions embedded in supplied content.
/// </remarks>
public sealed class EnhanceContentPromptBuilder : IEnhanceContentPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <inheritdoc />
public string Build(EnhanceContentRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.ContentKind,
            request.TargetField,
            request.CurrentText,
            request.UserPrompt,
            request.Title,
            request.Summary,
            request.Slug,
            request.Tone,
            request.Metadata
        }, JsonOptions);

        return $$"""
            Improve the requested AeroCMS content field.

            Return a JSON object with:
            - enhancedText: the improved field text only
            - rationale: a short explanation of the edit, or null
            - warnings: an array of warnings, empty when there are none

            Keep warnings conservative. Do not include markdown fences around the JSON.

            Input:
            {{payload}}
            """;
    }
}
