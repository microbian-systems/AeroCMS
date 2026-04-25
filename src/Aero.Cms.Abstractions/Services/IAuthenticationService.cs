using Aero.Models;
using Aero.Models.Entities;

namespace Aero.Cms.Abstractions.Services;

/// <summary>
/// Service for managing multi-provider authentication.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user using the best matching strategy.
    /// </summary>
    /// <param name="request">The authentication request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authenticated user or null if authentication fails.</returns>
    Task<AeroUser?> AuthenticateAsync(IAuthRequestModel request, CancellationToken cancellationToken = default);
}
