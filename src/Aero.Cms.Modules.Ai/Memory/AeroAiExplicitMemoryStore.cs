using Aero.Cms.Abstractions.Ai.Memory;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Ai.Memory;

/// <summary>
/// Persists only caller-confirmed memories and never derives memories from conversation frequency.
/// </summary>
public sealed class AeroAiExplicitMemoryStore(
    IDocumentSession session,
    ILogger<AeroAiExplicitMemoryStore> logger)
    : IAeroAiExplicitMemoryStore
{
    public async Task<Result<AeroAiExplicitMemory>> SaveAsync(
        AeroAiMemoryScope scope,
        AeroAiExplicitMemoryWrite memory,
        CancellationToken cancellationToken = default)
    {
        var error = Validate(scope, memory);
        if (error is not null)
            return error;

        try
        {
            if (memory.SourceConversationId is not null)
            {
                var conversationExists = await session.Query<AeroAiConversationDocument>()
                    .FirstOrDefaultAsync(conversation =>
                        conversation.Id == memory.SourceConversationId
                        && conversation.TenantId == scope.TenantId
                        && conversation.SiteId == scope.SiteId
                        && conversation.Audience == scope.Audience
                        && conversation.PrincipalKind == scope.PrincipalKind
                        && conversation.PrincipalId == scope.PrincipalId
                        && conversation.Culture == scope.Culture,
                        cancellationToken);
                if (conversationExists is null)
                    return AeroError.InvalidRequestError("The memory source is unavailable.");

                if (memory.SourceMessageId is not null)
                {
                    var messageExists = await session.Query<AeroAiConversationMessageDocument>()
                        .FirstOrDefaultAsync(message =>
                            message.Id == memory.SourceMessageId
                            && message.ConversationId == memory.SourceConversationId
                            && message.TenantId == scope.TenantId
                            && message.SiteId == scope.SiteId
                            && message.Audience == scope.Audience
                            && message.PrincipalKind == scope.PrincipalKind
                            && message.PrincipalId == scope.PrincipalId
                            && message.Culture == scope.Culture,
                            cancellationToken);
                    if (messageExists is null)
                        return AeroError.InvalidRequestError("The memory source is unavailable.");
                }
            }

            var now = PersistentTimestampUtcNow();
            if (memory.MemoryId is long memoryId)
            {
                var existingDocument = await ScopedMemories(scope)
                    .FirstOrDefaultAsync(candidate => candidate.Id == memoryId, cancellationToken);
                if (existingDocument is null)
                    return AeroError.InvalidRequestError("The explicit memory is unavailable.");

                existingDocument.Label = memory.Label.Trim();
                existingDocument.Content = memory.Content.Trim();
                existingDocument.SourceConversationId = memory.SourceConversationId;
                existingDocument.SourceMessageId = memory.SourceMessageId;
                existingDocument.ModifiedOn = now;
                session.Store(existingDocument);
                await session.SaveChangesAsync(cancellationToken);
                return Map(existingDocument);
            }

            var existing = await ScopedMemories(scope)
                .Take(AeroAiMemoryLimits.MaximumExplicitMemories)
                .ToListAsync(cancellationToken);
            if (existing.Count >= AeroAiMemoryLimits.MaximumExplicitMemories)
            {
                return AeroError.ValidationError(
                    [$"At most {AeroAiMemoryLimits.MaximumExplicitMemories} explicit memories are allowed per site."]);
            }

            var document = new AeroAiExplicitMemoryDocument
            {
                Id = Snowflake.NewId(),
                TenantId = scope.TenantId,
                SiteId = scope.SiteId,
                Audience = scope.Audience,
                PrincipalKind = scope.PrincipalKind,
                PrincipalId = scope.PrincipalId,
                Culture = scope.Culture,
                Label = memory.Label.Trim(),
                Content = memory.Content.Trim(),
                SourceConversationId = memory.SourceConversationId,
                SourceMessageId = memory.SourceMessageId,
                CreatedOn = now,
                ModifiedOn = now
            };
            session.Store(document);
            await session.SaveChangesAsync(cancellationToken);
            return Map(document);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to save explicit AI memory. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("The explicit memory could not be saved.");
        }
    }

    public async Task<Result<IReadOnlyList<AeroAiExplicitMemory>>> ListAsync(
        AeroAiMemoryScope scope,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var scopeError = AeroAiConversationStore.ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (take is < 1 or > AeroAiMemoryLimits.MaximumMemoryListTake)
        {
            return AeroError.ValidationError(
                [$"Take must be between 1 and {AeroAiMemoryLimits.MaximumMemoryListTake}."]);
        }

        try
        {
            var documents = await ScopedMemories(scope)
                .OrderByDescending(document => document.ModifiedOn)
                .Take(take)
                .ToListAsync(cancellationToken);
            return documents.Select(Map).ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to list explicit AI memories. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("Explicit memories could not be loaded.");
        }
    }

    public async Task<Result<bool>> DeleteAsync(
        AeroAiMemoryScope scope,
        long memoryId,
        CancellationToken cancellationToken = default)
    {
        var scopeError = AeroAiConversationStore.ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        if (memoryId <= 0)
            return AeroError.ValidationError(["Memory identifiers must be positive."]);

        try
        {
            var document = await ScopedMemories(scope)
                .FirstOrDefaultAsync(memory => memory.Id == memoryId, cancellationToken);
            if (document is null)
                return AeroError.InvalidRequestError("The explicit memory is unavailable.");

            session.Delete(document);
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to delete explicit AI memory. TenantId={TenantId} SiteId={SiteId} PrincipalId={PrincipalId}",
                scope.TenantId,
                scope.SiteId,
                scope.PrincipalId);
            return AeroError.CreateError("The explicit memory could not be deleted.");
        }
    }

    private IQueryable<AeroAiExplicitMemoryDocument> ScopedMemories(AeroAiMemoryScope scope)
        => session.Query<AeroAiExplicitMemoryDocument>()
            .Where(memory =>
                memory.TenantId == scope.TenantId
                && memory.SiteId == scope.SiteId
                && memory.Audience == scope.Audience
                && memory.PrincipalKind == scope.PrincipalKind
                && memory.PrincipalId == scope.PrincipalId
                && memory.Culture == scope.Culture);

    private static AeroError? Validate(
        AeroAiMemoryScope scope,
        AeroAiExplicitMemoryWrite memory)
    {
        var scopeError = AeroAiConversationStore.ValidateScope(scope);
        if (scopeError is not null)
            return scopeError;
        ArgumentNullException.ThrowIfNull(memory);
        if (string.IsNullOrWhiteSpace(memory.Label) ||
            memory.Label.Length > AeroAiMemoryLimits.MaximumMemoryLabelCharacters)
        {
            return AeroError.ValidationError(["The memory label is invalid."]);
        }
        if (string.IsNullOrWhiteSpace(memory.Content) ||
            memory.Content.Length > AeroAiMemoryLimits.MaximumMemoryContentCharacters)
        {
            return AeroError.ValidationError(["The memory content is invalid."]);
        }
        if (memory.SourceConversationId is <= 0 || memory.SourceMessageId is <= 0)
            return AeroError.ValidationError(["Memory source identifiers must be positive."]);
        if (memory.SourceMessageId is not null && memory.SourceConversationId is null)
            return AeroError.ValidationError(["A source message requires a source conversation."]);
        if (memory.MemoryId is <= 0)
            return AeroError.ValidationError(["Memory identifiers must be positive."]);
        return null;
    }

    private static AeroAiExplicitMemory Map(AeroAiExplicitMemoryDocument document)
        => new(
            document.Id,
            document.Label,
            document.Content,
            document.SourceConversationId,
            document.SourceMessageId,
            document.CreatedOn,
            document.ModifiedOn);

    private static DateTimeOffset PersistentTimestampUtcNow()
        => DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
}
