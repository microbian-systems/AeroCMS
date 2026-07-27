using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Memory;

/// <summary>
/// Stores bounded server-owned conversation history with full-scope predicates on every lookup.
/// </summary>
public sealed class AeroAiConversationStore(
    IDocumentSession session,
    ILogger<AeroAiConversationStore> logger)
    : IAeroAiConversationStore
{
    public async Task<Result<AeroAiConversationTurn>> BeginTurnAsync(
        AeroAiMemoryScope scope,
        long? conversationId,
        IReadOnlyList<AeroCmsAssistantMessage> requestMessages,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(scope, requestMessages, correlationId);
        if (validation is not null)
            return validation;
        if (conversationId is <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);

        try
        {
            var now = DateTimeOffset.UtcNow;
            AeroAiConversationDocument conversation;
            IReadOnlyList<AeroCmsAssistantMessage> providerMessages;
            if (conversationId is null)
            {
                conversation = NewConversation(
                    scope,
                    now,
                    requestMessages.FirstOrDefault(message =>
                        message.Role == AeroCmsAssistantRole.User)?.Content);
                session.Store(conversation);
                foreach (var message in requestMessages)
                {
                    conversation.LastMessageSequence++;
                    session.Store(NewMessage(
                        conversation.Id,
                        conversation.LastMessageSequence,
                        scope,
                        message,
                        correlationId,
                        now));
                }
                providerMessages = requestMessages.ToArray();
            }
            else
            {
                conversation = await FindConversationAsync(
                        scope,
                        conversationId.Value,
                        cancellationToken)
                    ?? throw new ScopedConversationNotFoundException();

                var userMessage = requestMessages[^1];
                var history = await LoadRecentMessagesAsync(
                    scope,
                    conversation.Id,
                    AeroCmsAssistantLimits.MaxMessages - 1,
                    AeroCmsAssistantLimits.MaxConversationCharacters - userMessage.Content.Length,
                    cancellationToken);
                providerMessages = history
                    .Select(ToMessage)
                    .Append(userMessage)
                    .ToArray();

                await DeleteOldestIfAtCapacityAsync(scope, conversation.Id, cancellationToken);
                conversation.LastMessageSequence++;
                session.Store(NewMessage(
                    conversation.Id,
                    conversation.LastMessageSequence,
                    scope,
                    userMessage,
                    correlationId,
                    now));
                conversation.ModifiedOn = now;
                session.Store(conversation);
            }

            await session.SaveChangesAsync(cancellationToken);
            return new AeroAiConversationTurn(conversation.Id, providerMessages);
        }
        catch (ScopedConversationNotFoundException)
        {
            return AeroError.InvalidRequestError(
                "The conversation is unavailable in the current security scope.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to begin scoped AI conversation turn. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("Conversation history could not be persisted.");
        }
    }

    public async Task<Result<bool>> AppendAssistantMessageAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        string content,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var scopeError = ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (conversationId <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > AeroCmsAssistantLimits.MaxOutputCharacters)
        {
            return AeroError.ValidationError(["The assistant message is invalid."]);
        }
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            return AeroError.ValidationError(["The correlation identifier is invalid."]);

        try
        {
            var conversation = await FindConversationAsync(
                    scope,
                    conversationId,
                    cancellationToken)
                ?? throw new ScopedConversationNotFoundException();
            var now = DateTimeOffset.UtcNow;
            await DeleteOldestIfAtCapacityAsync(scope, conversation.Id, cancellationToken);
            conversation.LastMessageSequence++;
            session.Store(NewMessage(
                conversation.Id,
                conversation.LastMessageSequence,
                scope,
                new AeroCmsAssistantMessage(AeroCmsAssistantRole.Assistant, content),
                correlationId,
                now));
            conversation.ModifiedOn = now;
            session.Store(conversation);
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (ScopedConversationNotFoundException)
        {
            return AeroError.InvalidRequestError(
                "The conversation is unavailable in the current security scope.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to append scoped AI conversation response. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("Conversation history could not be persisted.");
        }
    }

    public async Task<Result<IReadOnlyList<AeroCmsAssistantConversationSummary>>> ListAsync(
        AeroAiMemoryScope scope,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var scopeError = ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (take is < 1 or > AeroAiMemoryConstants.MaximumConversationListTake)
        {
            return AeroError.ValidationError(
                [$"Take must be between 1 and {AeroAiMemoryConstants.MaximumConversationListTake}."]);
        }

        try
        {
            var conversations = await ScopedConversations(scope)
                .OrderByDescending(conversation => conversation.ModifiedOn)
                .Take(take)
                .ToListAsync(cancellationToken);
            return conversations
                .Select(conversation => new AeroCmsAssistantConversationSummary(
                    conversation.Id,
                    conversation.Title,
                    conversation.CreatedOn,
                    conversation.ModifiedOn))
                .ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to list scoped AI conversations. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("Conversation history could not be loaded.");
        }
    }

    public async Task<Result<AeroCmsAssistantConversation>> GetAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var scopeError = ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (conversationId <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);

        try
        {
            var conversation = await FindConversationAsync(scope, conversationId, cancellationToken);
            if (conversation is null)
            {
                return AeroError.InvalidRequestError(
                    "The conversation is unavailable in the current security scope.");
            }

            var messages = await ScopedMessages(scope, conversationId)
                .OrderBy(message => message.Sequence)
                .Take(AeroCmsAssistantLimits.MaxStoredMessages)
                .ToListAsync(cancellationToken);
            return new AeroCmsAssistantConversation(
                conversation.Id,
                conversation.Title,
                messages.Select(ToMessage).ToArray(),
                conversation.CreatedOn,
                conversation.ModifiedOn);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to load scoped AI conversation. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("Conversation history could not be loaded.");
        }
    }

    public async Task<Result<bool>> DeleteAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var scopeError = ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (conversationId <= 0)
            return AeroError.ValidationError(["Conversation identifiers must be positive."]);

        try
        {
            var conversation = await FindConversationAsync(scope, conversationId, cancellationToken);
            if (conversation is null)
            {
                return AeroError.InvalidRequestError(
                    "The conversation is unavailable in the current security scope.");
            }

            var messages = await ScopedMessages(scope, conversationId)
                .Take(AeroCmsAssistantLimits.MaxStoredMessages)
                .ToListAsync(cancellationToken);
            var sourcedMemories = await session.Query<AeroAiExplicitMemoryDocument>()
                .Where(memory =>
                    memory.TenantId == scope.TenantId
                    && memory.SiteId == scope.SiteId
                    && memory.Audience == scope.Audience
                    && memory.PrincipalKind == scope.PrincipalKind
                    && memory.PrincipalId == scope.PrincipalId
                    && memory.Culture == scope.Culture
                    && memory.SourceConversationId == conversationId)
                .Take(AeroAiMemoryLimits.MaximumExplicitMemories)
                .ToListAsync(cancellationToken);
            foreach (var message in messages)
                session.Delete(message);
            foreach (var memory in sourcedMemories)
                session.Delete(memory);
            session.Delete(conversation);
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to delete scoped AI conversation. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("Conversation history could not be deleted.");
        }
    }

    private async Task<AeroAiConversationDocument?> FindConversationAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        CancellationToken cancellationToken)
        => await session.Query<AeroAiConversationDocument>()
            .FirstOrDefaultAsync(conversation =>
                conversation.Id == conversationId
                && conversation.TenantId == scope.TenantId
                && conversation.SiteId == scope.SiteId
                && conversation.Audience == scope.Audience
                && conversation.PrincipalKind == scope.PrincipalKind
                && conversation.PrincipalId == scope.PrincipalId
                && conversation.Culture == scope.Culture
                && conversation.ArchivedOn == null,
                cancellationToken);

    private IQueryable<AeroAiConversationDocument> ScopedConversations(
        AeroAiMemoryScope scope)
        => session.Query<AeroAiConversationDocument>()
            .Where(conversation =>
                conversation.TenantId == scope.TenantId
                && conversation.SiteId == scope.SiteId
                && conversation.Audience == scope.Audience
                && conversation.PrincipalKind == scope.PrincipalKind
                && conversation.PrincipalId == scope.PrincipalId
                && conversation.Culture == scope.Culture
                && conversation.ArchivedOn == null);

    private async Task<List<AeroAiConversationMessageDocument>> LoadRecentMessagesAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        int take,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var messages = await ScopedMessages(scope, conversationId)
            .OrderByDescending(message => message.Sequence)
            .Take(take)
            .ToListAsync(cancellationToken);
        var selected = new List<AeroAiConversationMessageDocument>(messages.Count);
        var characters = 0;
        foreach (var message in messages)
        {
            if (characters + message.Content.Length > maximumCharacters)
                break;
            selected.Add(message);
            characters += message.Content.Length;
        }
        selected.Reverse();
        return selected;
    }

    private async Task DeleteOldestIfAtCapacityAsync(
        AeroAiMemoryScope scope,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var messages = await ScopedMessages(scope, conversationId)
            .OrderByDescending(message => message.Sequence)
            .Take(AeroCmsAssistantLimits.MaxStoredMessages)
            .ToListAsync(cancellationToken);
        if (messages.Count >= AeroCmsAssistantLimits.MaxStoredMessages)
            session.Delete(messages[^1]);
    }

    private IQueryable<AeroAiConversationMessageDocument> ScopedMessages(
        AeroAiMemoryScope scope,
        long conversationId)
        => session.Query<AeroAiConversationMessageDocument>()
            .Where(message =>
                message.ConversationId == conversationId
                && message.TenantId == scope.TenantId
                && message.SiteId == scope.SiteId
                && message.Audience == scope.Audience
                && message.PrincipalKind == scope.PrincipalKind
                && message.PrincipalId == scope.PrincipalId
                && message.Culture == scope.Culture);

    private static AeroAiConversationDocument NewConversation(
        AeroAiMemoryScope scope,
        DateTimeOffset now,
        string? title = null)
        => new()
        {
            Id = Snowflake.NewId(),
            TenantId = scope.TenantId,
            SiteId = scope.SiteId,
            Audience = scope.Audience,
            PrincipalKind = scope.PrincipalKind,
            PrincipalId = scope.PrincipalId,
            Culture = scope.Culture,
            Title = CreateTitle(title),
            CreatedOn = now,
            ModifiedOn = now
        };

    private static AeroAiConversationMessageDocument NewMessage(
        long conversationId,
        long sequence,
        AeroAiMemoryScope scope,
        AeroCmsAssistantMessage message,
        string correlationId,
        DateTimeOffset now)
        => new()
        {
            Id = Snowflake.NewId(),
            ConversationId = conversationId,
            TenantId = scope.TenantId,
            SiteId = scope.SiteId,
            Audience = scope.Audience,
            PrincipalKind = scope.PrincipalKind,
            PrincipalId = scope.PrincipalId,
            Culture = scope.Culture,
            Sequence = sequence,
            Role = message.Role,
            Content = message.Content,
            CorrelationId = correlationId,
            CreatedOn = now
        };

    private static AeroCmsAssistantMessage ToMessage(
        AeroAiConversationMessageDocument message)
        => new(message.Role, message.Content);

    private static string CreateTitle(string? value)
    {
        var title = string.IsNullOrWhiteSpace(value)
            ? "New conversation"
            : value.Trim();
        return title.Length <= AeroAiMemoryConstants.MaximumConversationTitleCharacters
            ? title
            : title[..AeroAiMemoryConstants.MaximumConversationTitleCharacters];
    }

    private static AeroError? Validate(
        AeroAiMemoryScope scope,
        IReadOnlyList<AeroCmsAssistantMessage>? messages,
        string correlationId)
    {
        var scopeError = ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (messages is null || messages.Count == 0 ||
            messages.Count > AeroCmsAssistantLimits.MaxMessages)
        {
            return AeroError.ValidationError(["Conversation messages are invalid."]);
        }
        if (messages[^1].Role != AeroCmsAssistantRole.User)
            return AeroError.ValidationError(["The final conversation message must be from the user."]);
        if (messages.Any(message =>
                !Enum.IsDefined(message.Role)
                || string.IsNullOrWhiteSpace(message.Content)))
        {
            return AeroError.ValidationError(["Conversation messages are invalid."]);
        }
        var characters = 0;
        foreach (var message in messages)
        {
            if (message.Role == AeroCmsAssistantRole.User &&
                message.Content.Length > AeroCmsAssistantLimits.MaxUserMessageCharacters)
            {
                return AeroError.ValidationError(["A user message exceeds the allowed size."]);
            }
            characters = checked(characters + message.Content.Length);
            if (characters > AeroCmsAssistantLimits.MaxConversationCharacters)
                return AeroError.ValidationError(["Conversation messages exceed the allowed size."]);
        }
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            return AeroError.ValidationError(["The correlation identifier is invalid."]);
        return null;
    }

    internal static AeroError? ValidateScope(AeroAiMemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.TenantId <= 0 || scope.SiteId <= 0 || scope.PrincipalId <= 0)
            return AeroError.ForbiddenError("A durable AI identity scope is required.");
        if (string.IsNullOrWhiteSpace(scope.Culture) || scope.Culture.Length > 32)
            return AeroError.ValidationError(["The memory culture is invalid."]);
        if (scope.Audience == AeroAiAudience.Manager &&
            scope.PrincipalKind != AeroAiPrincipalKind.ManagerUser)
        {
            return AeroError.ForbiddenError("The manager memory scope is invalid.");
        }
        if (scope.Audience == AeroAiAudience.Member &&
            scope.PrincipalKind != AeroAiPrincipalKind.Member)
        {
            return AeroError.ForbiddenError("The member memory scope is invalid.");
        }
        if (scope.Audience is not (AeroAiAudience.Manager or AeroAiAudience.Member))
            return AeroError.ForbiddenError("Anonymous and MCP conversations are not durable.");
        return null;
    }

    private sealed class ScopedConversationNotFoundException : Exception;
}
