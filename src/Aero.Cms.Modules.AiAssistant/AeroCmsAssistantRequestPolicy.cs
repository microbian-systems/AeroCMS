using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.AiAssistant;

/// <summary>Applies the stateless conversation bounds before any provider access.</summary>
public static class AeroCmsAssistantRequestPolicy
{
    public static Result<IReadOnlyList<AeroCmsAssistantMessage>> Validate(AeroCmsAssistantRequest? request)
    {
        if (request?.Messages is null || request.Messages.Count == 0)
            return AeroError.ValidationError(["At least one conversation message is required."]);
        if (request.ConversationId is <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);
        if (request.Messages.Count > AeroCmsAssistantLimits.MaxMessages)
            return AeroError.ValidationError([$"A conversation can contain at most {AeroCmsAssistantLimits.MaxMessages} messages."]);

        var total = 0;
        foreach (var message in request.Messages)
        {
            if (!Enum.IsDefined(message.Role))
                return AeroError.ValidationError(["The conversation contains an unsupported role."]);
            if (string.IsNullOrWhiteSpace(message.Content))
                return AeroError.ValidationError(["Conversation messages cannot be empty."]);
            if (message.Role == AeroCmsAssistantRole.User &&
                message.Content.Length > AeroCmsAssistantLimits.MaxUserMessageCharacters)
            {
                return AeroError.ValidationError(
                    [$"User messages cannot exceed {AeroCmsAssistantLimits.MaxUserMessageCharacters} characters."]);
            }

            total = checked(total + message.Content.Length);
            if (total > AeroCmsAssistantLimits.MaxConversationCharacters)
                return AeroError.ValidationError([$"Conversation history cannot exceed {AeroCmsAssistantLimits.MaxConversationCharacters} characters."]);
        }

        if (request.Messages[^1].Role != AeroCmsAssistantRole.User)
            return AeroError.ValidationError(["The final conversation message must be from the user."]);

        return new Result<IReadOnlyList<AeroCmsAssistantMessage>>.Ok(request.Messages);
    }
}
