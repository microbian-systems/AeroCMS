using Aero.Cms.Abstractions.Http.Clients;
using Aero.Models;
using Aero.Models.Entities;

namespace Aero.Cms.Abstractions.Services;

/// <summary>
/// Strategy for authenticating users through different providers (Local, API Key, WorkOS, etc.)
/// </summary>
public interface IAuthenticationStrategy
{
    /// <summary>
    /// Gets the type of authentication this strategy supports.
    /// </summary>
    string AuthType { get; }

    /// <summary>
    /// Authenticates a user based on the provided request model.
    /// </summary>
    /// <param name="request">The authentication request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authenticated user or null if authentication fails.</returns>
    Task<AeroUser?> AuthenticateAsync(ApiKeyAuthRequest request, CancellationToken cancellationToken = default);
}
