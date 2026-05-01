using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models;
using Aero.Models.Entities;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Composite authentication service that delegates to multiple strategies.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IEnumerable<IAuthenticationStrategy> _strategies;

    public AuthenticationService(IEnumerable<IAuthenticationStrategy> strategies)
    {
        _strategies = strategies;
    }

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
