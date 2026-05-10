using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases.Events;

/// <summary>
/// Published when an existing alias is updated. Triggers cache invalidation.
/// </summary>
public sealed record AliasUpdated(AliasDocument Document);
