namespace Aero.Cms.Abstractions.Audit;

/// <summary>
/// Interface for the audit logging service that records all CMS events.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit event asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">The type of the audit event.</typeparam>
    /// <param name="auditEvent">The audit event to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<bool, AeroError>> LogAsync<TEvent>(
        TEvent auditEvent,
        CancellationToken cancellationToken = default) where TEvent : AuditEvent;
}
