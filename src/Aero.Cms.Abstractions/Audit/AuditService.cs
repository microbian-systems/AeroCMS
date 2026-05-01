using Microsoft.Extensions.Logging;

namespace Aero.Cms.Abstractions.Audit;


// todo - figure out the proper place (project) where the auditservice should live

/// <summary>
/// Default implementation of <see cref="IAuditService"/> that logs audit events.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> LogAsync<TEvent>(
        TEvent auditEvent,
        CancellationToken cancellationToken = default) where TEvent : AuditEvent
    {
        try
        {
            _logger.LogInformation(
                "Audit: [{EventType}] {EntityType}({EntityId}) by User {UserId} at {Timestamp}",
                auditEvent.EventType,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.UserId,
                auditEvent.Timestamp);

            if (auditEvent.Metadata is { Count: > 0 })
            {
                foreach (var (key, value) in auditEvent.Metadata)
                {
                    _logger.LogDebug("Audit metadata: {Key} = {Value}", key, value);
                }
            }

            return Task.FromResult<Result<bool, AeroError>>(new Result<bool, AeroError>.Ok(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event: [{EventType}] {EntityType}({EntityId})",
                auditEvent.EventType,
                auditEvent.EntityType,
                auditEvent.EntityId);

            return Task.FromResult<Result<bool, AeroError>>(
                new Result<bool, AeroError>.Failure(AeroError.CreateError("Failed to log audit event")));
        }
    }
}
