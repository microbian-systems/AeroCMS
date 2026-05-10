using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases.Events;

/// <summary>
/// Published when an alias is deleted. Triggers cache invalidation.
/// </summary>
public sealed record AliasDeleted(AliasDocument Document);
