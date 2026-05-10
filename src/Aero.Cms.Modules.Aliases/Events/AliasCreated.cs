using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases.Events;

/// <summary>
/// Published when a new alias is created. Triggers cache invalidation.
/// </summary>
public sealed record AliasCreated(AliasDocument Document);
