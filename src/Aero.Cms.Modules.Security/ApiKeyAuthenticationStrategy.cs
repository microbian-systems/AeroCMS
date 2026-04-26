using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models;
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

    public ApiKeyAuthenticationStrategy(IApiKeyService apiKeyService, UserManager<AeroUser> userManager)
    {
        _apiKeyService = apiKeyService;
        _userManager = userManager;
    }

    public string AuthType => "ApiKey";

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
