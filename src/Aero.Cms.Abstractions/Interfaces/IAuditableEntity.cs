namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Marker interface for entities that should be tracked by the Audit module.
/// Implementing this interface enables automatic audit logging via
/// <c>IAuditableEntityDocumentSessionListener</c> — no audit configuration needed
/// in the entity itself.
/// </summary>
public interface IAuditableEntity;
