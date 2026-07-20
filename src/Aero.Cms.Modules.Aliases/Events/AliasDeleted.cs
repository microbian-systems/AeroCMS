using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases.Events;

/// <summary>
/// Published after an alias deletion has been committed. Consumers may use it
/// to invalidate derived alias state; it does not itself guarantee delivery or
/// cache refresh completion.
/// </summary>
/// <param name="Document">The alias document that was deleted.</param>
public sealed record AliasDeleted(AliasDocument Document);
