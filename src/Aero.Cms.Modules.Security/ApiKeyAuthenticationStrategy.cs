using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Resolves an enabled API-account key to an active, non-deleted Identity user.
/// </summary>
/// <remarks>
/// A successful result is an <see cref="AeroUser"/> entity only; this strategy does not create a claims principal,
/// select an authentication scheme, authorize an endpoint, or apply a tenant boundary.
/// </remarks>
public sealed class ApiKeyAuthenticationStrategy : IAuthenticationStrategy
{
    private readonly IApiKeyService _apiKeyService;
    private readonly UserManager<AeroUser> _userManager;

        /// <summary>
    /// Initializes the strategy with API-key validation and Identity user lookup services.
    /// </summary>
public ApiKeyAuthenticationStrategy(IApiKeyService apiKeyService, UserManager<AeroUser> userManager)
    {
        _apiKeyService = apiKeyService;
        _userManager = userManager;
    }

        /// <summary>
    /// Gets the fixed strategy discriminator <c>ApiKey</c>.
    /// </summary>
public string AuthType => "ApiKey";

        /// <summary>
    /// Validates the request key, loads the resolved Identity user, and rejects inactive or soft-deleted users.
    /// </summary>
    /// <remarks>
    /// Cancellation is forwarded to key validation, but the subsequent <c>UserManager</c> lookup has no cancellation
    /// parameter. Operational exceptions are not converted to authentication failure and propagate to the caller.
    /// </remarks>
public async Task<AeroUser?> AuthenticateAsync(ApiKeyAuthRequest request, CancellationToken cancellationToken = default)
    {
        if (request is not ApiKeyAuthRequest apiKeyRequest)
        {
            return null;
        }

        var validation = await _apiKeyService.ValidateAsync(apiKeyRequest.ApiKey, cancellationToken);
        if (validation == null)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(validation.UserId.ToString());
        
        if (user != null && user.IsActive && !user.IsDeleted)
        {
            return user;
        }

        return null;
    }
}
