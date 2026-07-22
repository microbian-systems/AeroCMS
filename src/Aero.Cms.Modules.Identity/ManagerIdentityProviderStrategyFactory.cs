using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Identity;

public sealed class ManagerIdentityProviderStrategyFactory(
    IEnumerable<IManagerIdentityProviderStrategy> strategies) : IManagerIdentityProviderStrategyFactory
{
    private readonly IReadOnlyList<IManagerIdentityProviderStrategy> _strategies = strategies.ToArray();

    public Result<IManagerIdentityProviderStrategy, AeroError> Resolve(string provider)
    {
        if (!ManagerIdentityProviders.IsSupported(provider)) return Failed();
        var matches = _strategies.Where(strategy =>
            string.Equals(strategy.Provider, provider, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1
            ? Prelude.Ok<IManagerIdentityProviderStrategy, AeroError>(matches[0])
            : Failed();
    }

    private static Result<IManagerIdentityProviderStrategy, AeroError> Failed() =>
        Prelude.Fail<IManagerIdentityProviderStrategy, AeroError>(
            AeroError.CreateError("Manager identity provider is unavailable."));
}
