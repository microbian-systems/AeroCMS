using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Tries registered authentication strategies in dependency-injection enumeration order.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IEnumerable<IAuthenticationStrategy> _strategies;

        /// <summary>
    /// Initializes the service with the strategy sequence to evaluate.
    /// </summary>
public AuthenticationService(IEnumerable<IAuthenticationStrategy> strategies)
    {
        _strategies = strategies;
    }

        /// <summary>
    /// Returns the first user produced by a strategy, or <see langword="null"/> when every strategy declines.
    /// </summary>
    /// <remarks>
    /// The cancellation token is forwarded to each strategy. Strategy exceptions and cancellation propagate; this
    /// service does not create a principal, issue credentials, authorize the user, or isolate by tenant.
    /// </remarks>
public async Task<AeroUser?> AuthenticateAsync(ApiKeyAuthRequest request, CancellationToken cancellationToken = default)
    {
        // Try each strategy in order
        foreach (var strategy in _strategies)
        {
            var user = await strategy.AuthenticateAsync(request, cancellationToken);
            if (user != null)
            {
                return user;
            }
        }

        return null;
    }
}
