using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Identity;

public interface IExternalMemberProviderStrategyFactory
{
    Result<IExternalMemberProviderStrategy, AeroError> Resolve(string? provider);
}

/// <summary>Resolves only explicitly registered, canonical provider strategies.</summary>
public sealed class ExternalMemberProviderStrategyFactory : IExternalMemberProviderStrategyFactory
{
    private readonly Dictionary<string, IExternalMemberProviderStrategy> _strategies = new(StringComparer.Ordinal);
    private readonly HashSet<string> _duplicates = new(StringComparer.Ordinal);

    public ExternalMemberProviderStrategyFactory(IEnumerable<IExternalMemberProviderStrategy> strategies)
    {
        foreach (var strategy in strategies ?? [])
        {
            var provider = strategy?.Provider;
            if (strategy is null || !ExternalMemberProviders.IsSupported(provider) || !_strategies.TryAdd(provider!, strategy))
                _duplicates.Add(provider ?? string.Empty);
        }
    }

    public Result<IExternalMemberProviderStrategy, AeroError> Resolve(string? provider) =>
        !ExternalMemberProviders.IsSupported(provider) || _duplicates.Contains(provider) ||
        !_strategies.TryGetValue(provider, out var strategy) || !string.Equals(provider, strategy.Provider, StringComparison.Ordinal)
            ? Prelude.Fail<IExternalMemberProviderStrategy, AeroError>(AeroError.CreateError("External sign-in is unavailable."))
            : Prelude.Ok<IExternalMemberProviderStrategy, AeroError>(strategy);
}
