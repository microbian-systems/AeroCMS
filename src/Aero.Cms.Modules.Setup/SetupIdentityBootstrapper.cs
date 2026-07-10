using Aero.Cms.Core;
using Aero.Core;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Represents a record for SetupIdentityBootstrapRequest.
/// </summary>
public sealed record SetupIdentityBootstrapRequest(
    string AdminUserName,
    string AdminEmail,
    string Password);

/// <summary>
/// Represents a class for SetupIdentityBootstrapResult.
/// </summary>
public sealed class SetupIdentityBootstrapResult
{
        /// <summary>
    /// Gets or sets the Succeeded.
    /// </summary>
public bool Succeeded => Errors.Count == 0;
        /// <summary>
    /// Gets or sets the Created Admin.
    /// </summary>
public bool CreatedAdmin { get; init; }
        /// <summary>
    /// Gets or sets the Created Roles.
    /// </summary>
public bool CreatedRoles { get; init; }
        /// <summary>
    /// Gets or sets the Admin User.
    /// </summary>
public AeroUser? AdminUser { get; init; }
        /// <summary>
    /// Gets or sets the Errors.
    /// </summary>
public List<IdentityError> Errors { get; } = [];

        /// <summary>
    /// Failure method.
    /// </summary>
public static SetupIdentityBootstrapResult Failure(IEnumerable<IdentityError> errors)
    {
        var result = new SetupIdentityBootstrapResult();
        result.Errors.AddRange(errors);
        return result;
    }
}

/// <summary>
/// Defines an interface for ISetupIdentityBootstrapper.
/// </summary>
public interface ISetupIdentityBootstrapper
{
        /// <summary>
    /// BootstrapAsync method.
    /// </summary>
Task<SetupIdentityBootstrapResult> BootstrapAsync(SetupIdentityBootstrapRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for SetupIdentityBootstrapper.
/// </summary>
public sealed class SetupIdentityBootstrapper(
    UserManager<AeroUser> userManager) : ISetupIdentityBootstrapper
{
        /// <summary>
    /// BootstrapAsync method.
    /// </summary>
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
