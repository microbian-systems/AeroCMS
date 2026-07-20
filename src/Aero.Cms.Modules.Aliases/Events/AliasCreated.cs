using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Aliases.Events;

/// <summary>
/// Published after a new alias has been committed. Consumers may use it to
/// invalidate derived alias state; it does not itself guarantee delivery or
/// cache refresh completion.
/// </summary>
/// <param name="Document">The committed alias document.</param>
public sealed record AliasCreated(AliasDocument Document);
