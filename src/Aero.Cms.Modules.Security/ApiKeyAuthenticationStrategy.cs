using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Security;

/// <summary>
/// Authentication strategy that uses API keys to authenticate users.
/// </summary>
public sealed class ApiKeyAuthenticationStrategy : IAuthenticationStrategy
{
    private readonly IApiKeyService _apiKeyService;
    private readonly UserManager<AeroUser> _userManager;

        /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationStrategy"/> class.
    /// </summary>
public ApiKeyAuthenticationStrategy(IApiKeyService apiKeyService, UserManager<AeroUser> userManager)
    {
        _apiKeyService = apiKeyService;
        _userManager = userManager;
    }

        /// <summary>
    /// Gets or sets the Auth Type.
    /// </summary>
public string AuthType => "ApiKey";

        /// <summary>
    /// AuthenticateAsync method.
    /// </summary>
public async Task<AeroUser?> AuthenticateAsync(ApiKeyAuthRequest request, CancellationToken cancellationToken = default)
    {
        if (request is not ApiKeyAuthRequest apiKeyRequest)
        {
            return null;
        }

        var userId = await _apiKeyService.ValidateAsync(apiKeyRequest.ApiKey, cancellationToken);
        if (userId == null)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        
        if (user != null && user.IsActive && !user.IsDeleted)
        {
            return user;
        }

        return null;
    }
}
