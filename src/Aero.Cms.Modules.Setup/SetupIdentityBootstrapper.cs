using Aero.Cms.Core;
using Aero.Core;
using Aero.Core.Identity;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Setup;

public sealed record SetupIdentityBootstrapRequest(
    string AdminUserName,
    string AdminEmail,
    string Password);

public sealed class SetupIdentityBootstrapResult
{
    public bool Succeeded => Errors.Count == 0;
    public bool CreatedAdmin { get; init; }
    public bool CreatedRoles { get; init; }
    public AeroUser? AdminUser { get; init; }
    public List<IdentityError> Errors { get; } = [];

    public static SetupIdentityBootstrapResult Failure(IEnumerable<IdentityError> errors)
    {
        var result = new SetupIdentityBootstrapResult();
        result.Errors.AddRange(errors);
        return result;
    }
}

public interface ISetupIdentityBootstrapper
{
    Task<SetupIdentityBootstrapResult> BootstrapAsync(SetupIdentityBootstrapRequest request, CancellationToken cancellationToken = default);
}

public sealed class SetupIdentityBootstrapper(
    UserManager<AeroUser> userManager) : ISetupIdentityBootstrapper
{
    public async Task<SetupIdentityBootstrapResult> BootstrapAsync(SetupIdentityBootstrapRequest request, CancellationToken cancellationToken = default)
    {
        var existingAdmins = await userManager.GetUsersInRoleAsync(AeroCmsRoles.Admin);
        var adminUser = existingAdmins.FirstOrDefault();
        var createdAdmin = false;
        var createdRoles = false;

        if (adminUser == null)
        {
            adminUser = await userManager.FindByEmailAsync(request.AdminEmail);

            if (adminUser == null)
            {
                adminUser = new AeroUser
                {
                    Id = Snowflake.NewId(),
                    UserName = request.AdminUserName,
                    Email = request.AdminEmail,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var createAdminResult = await userManager.CreateAsync(adminUser, request.Password);
                if (!createAdminResult.Succeeded)
                {
                    return SetupIdentityBootstrapResult.Failure(createAdminResult.Errors);
                }

                createdAdmin = true;
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AeroCmsRoles.Admin))
        {
            createdRoles = true;
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, AeroCmsRoles.Admin);
            if (!addToRoleResult.Succeeded)
            {
                return SetupIdentityBootstrapResult.Failure(addToRoleResult.Errors);
            }
        }

        return new SetupIdentityBootstrapResult
        {
            AdminUser = adminUser,
            CreatedAdmin = createdAdmin,
            CreatedRoles = createdRoles
        };
    }
}
