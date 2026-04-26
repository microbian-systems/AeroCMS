using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

public static class AuthApi
{
    public static void MapAuthApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Headless - Auth");

        group.MapPost("/login", Login)
            .WithName("HeadlessLogin");
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] SignInManager<AeroUser> signInManager,
        [FromServices] UserManager<AeroUser> userManager,
        [FromServices] IApiKeyService apiKeyService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AuthApi));

        try
        {
            // Step 1: Validate username/password credentials
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user == null)
            {
                return TypedResults.Unauthorized();
            }

            var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!signInResult.Succeeded)
            {
                return TypedResults.Unauthorized();
            }

            if (!user.IsActive || user.IsDeleted)
            {
                return TypedResults.Unauthorized();
            }

            // Step 2: Create or retrieve API key (upsert)
            var apiKey = await apiKeyService.CreateKeyAsync(user.Id, user.Email!, cancellationToken: cancellationToken);

            // Step 3: Get user roles
            var roles = (await userManager.GetRolesAsync(user)).ToList();

            // Step 4: Return user info with API key
            return TypedResults.Ok(new AuthLoginResponse(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                $"{user.FirstName} {user.MiddleName} {user.LastName}".Trim(),
                roles,
                apiKey));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during headless login for user={UserName}", request.UserName);
            return TypedResults.Problem(ex.Message);
        }
    }
}
